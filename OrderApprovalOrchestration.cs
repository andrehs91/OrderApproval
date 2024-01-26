using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using FIAP.Function.Models;

namespace FIAP.Function;
public static class OrderApprovalOrchestration
{
    [Function(nameof(OrderApprovalOrchestration))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context, Order order)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(OrderApprovalOrchestration));
        logger.LogInformation("Fluxo de aprovação de um pedido.");

        bool itemsHaveBeenReserved = await context.CallActivityAsync<bool>("ReserveItems", order.Number);
        if (!itemsHaveBeenReserved) return "Um ou mais itens do pedido não puderam ser reservados.";

        using CancellationTokenSource cts = new();
        DateTime deadline = context.CurrentUtcDateTime.AddSeconds(120);
        Task timer = context.CreateTimer(deadline, cts.Token);

        Task<bool> paymentConfirmed = context.WaitForExternalEvent<bool>("PaymentConfirmed");
        if (paymentConfirmed == await Task.WhenAny(paymentConfirmed, timer))
        {
            cts.Cancel();
            if (paymentConfirmed.Result) return await context.CallActivityAsync<string>("CreateShipment", order.Number);
        }
        return await context.CallActivityAsync<string>("CancelOrderDueNonPayment", order.Number);
    }

    [Function(nameof(ReserveItems))]
    public static bool ReserveItems([ActivityTrigger] long orderNumber, FunctionContext executionContext)
    {
        var id = executionContext.FunctionId;
        ILogger logger = executionContext.GetLogger("ReserveItems");
        logger.LogInformation("Reservando os itens do pedido de número {orderNumber}.", orderNumber);
        // Lógica de reserva de itens...
        return orderNumber % 2 == 0;
    }

    [Function(nameof(PaymentConfirmed))]
    public static bool PaymentConfirmed([ActivityTrigger] bool paymentMade, FunctionContext executionContext)
    {
        var id = executionContext.FunctionId;
        ILogger logger = executionContext.GetLogger("PaymentConfirmed");
        logger.LogInformation("Confirmando o pagamento da orquestração de ID {id}.", id);
        return paymentMade;
    }

    [Function(nameof(CreateShipment))]
    public static string CreateShipment([ActivityTrigger] long orderNumber, FunctionContext executionContext)
    {
        var id = executionContext.FunctionId;
        ILogger logger = executionContext.GetLogger("CreateShipment");
        logger.LogInformation("Enviando o pedido de número {orderNumber}.", orderNumber);
        return $"O pedido de número {orderNumber} foi enviado.";
    }

    [Function(nameof(CancelOrderDueNonPayment))]
    public static string CancelOrderDueNonPayment([ActivityTrigger] long orderNumber, FunctionContext executionContext)
    {
        var id = executionContext.FunctionId;
        ILogger logger = executionContext.GetLogger("CancelOrderDueNonPayment");
        logger.LogInformation("Cancelando o pedido de número {orderNumber}.", orderNumber);
        // Lógica de cancelamento...
        return $"O pedido de número {orderNumber} foi cancelado por falta de pagamento.";
    }

    [Function("OrderApprovalOrchestration_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("OrderApprovalOrchestration_HttpStart");

        StreamReader reader = new(req.Body);
        string json = reader.ReadToEnd();
        Order? order = null;
        try
        {
            order = JsonSerializer.Deserialize<Order>(json);
        }
        catch (Exception) { }
        if (order is null)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString("O número do pedido não foi informado.");
            return response;
        }

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(OrderApprovalOrchestration), order);

        logger.LogInformation("Orquestração iniciada com ID = '{instanceId}'.", instanceId);

        return client.CreateCheckStatusResponse(req, instanceId);
    }
}

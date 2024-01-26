using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace FIAP.Function;
public static class ConfirmPayment
{
    [Function("ConfirmPayment")]
    [QueueOutput("payment-confirmed", Connection = "safiaptechchallenge_STORAGE")]
    public static string Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "confirm-payment/{instanceId}/{paymentMade}")] HttpRequestData req,
        string instanceId, bool paymentMade)
    {
        var message = new { instanceId, paymentMade };
        return JsonSerializer.Serialize(message);
    }
}

using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using FIAP.Function.Models;

namespace FIAP.Function;
public class PaymentConfirmedQueueProcessor
{
    [Function(nameof(PaymentConfirmedQueueProcessor))]
    public static async Task Run(
        [QueueTrigger("payment-confirmed", Connection = "safiaptechchallenge_STORAGE")] QueueMessage queueMessage,
        [DurableClient] DurableTaskClient client)
    {
        string messageBody = queueMessage.Body.ToString();
        Message message = JsonSerializer.Deserialize<Message>(messageBody)
            ?? throw new Exception("Não foi possível desserializar a mensagem recebida.");
        if (string.IsNullOrEmpty(message.instanceId))
            throw new Exception("O parâmetro 'instanceId' não foi informado.");
        await client.RaiseEventAsync(message.instanceId, "PaymentConfirmed", message.paymentMade);
    }
}

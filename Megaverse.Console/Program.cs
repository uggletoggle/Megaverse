using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using static System.Console;

WriteLine("--> Starting X Shape creation process...");

const int megaverseSize = 10;
const int initialXCoordinate = 2;
Guid CandidateId = Guid.Parse("d8f410a9-e5e4-492a-8a32-e2faeaffa9bb");

ConcurrentBag<Coordinate> coordinates = new();
var channel = Channel.CreateBounded<Coordinate>(megaverseSize);

WriteLine("--> Producing coordinates to generate X shape");

Parallel.For(initialXCoordinate, megaverseSize - 1, (i) =>
{
    var j = megaverseSize - i;
    var coordinateLeft = new Coordinate(i, i, CandidateId);
    var coordinateRight = coordinateLeft with { column = j };

    coordinates.Add(coordinateLeft);

    if (coordinateLeft != coordinateRight)
    {
        coordinates.Add(coordinateRight);
    }
});

WriteLine("--> Generating producer and consumer tasks");

Task consumer = ProcessData(channel.Reader);

Task producer = ProduceData(coordinates, channel.Writer);

WriteLine("--> Initiating Producer");
await producer;

WriteLine("--> Initiating Consumer");
await consumer;

async Task ProcessData(ChannelReader<Coordinate> reader)
{
    await foreach (Coordinate coordinate in reader.ReadAllAsync())
    {
        var success = false;
        while (!success)
        {
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri("https://challenge.crossmint.io/api/") };
                var response = await GeneratePolyanet(coordinate, client);
                response.EnsureSuccessStatusCode();
                success = true;
            }
            catch (Exception ex)
            {
                WriteLine($"WARNING --> error try to call remote server: {ex.Message} | Full Exception Details: {ex}");
                WriteLine("Waiting 5 sec to retry...");

                await Task.Delay(5000);

                WriteLine($"Trying to reprocess: {coordinate}");
            }
        }
    }
}

async Task ProduceData(IReadOnlyCollection<Coordinate> coordinates, ChannelWriter<Coordinate> writer)
{
    List<ValueTask> writeTasks = new();

    foreach (var coordinate in coordinates)
    {
        await writer.WriteAsync(coordinate);
    }

    writer.Complete();
}

Task<HttpResponseMessage> GeneratePolyanet(Coordinate coordinate, HttpClient client) =>
    client.PostAsync("polyanets", new StringContent($"{JsonSerializer.Serialize(coordinate)}",
        Encoding.UTF8,
        "application/json"));

Task<HttpResponseMessage> DeletePolyanet(Coordinate coordinate, HttpClient client) => client.SendAsync(new HttpRequestMessage
{
    Content = new StringContent($"{JsonSerializer.Serialize(coordinate)}",
        Encoding.UTF8,
        "application/json"),
    RequestUri = new Uri(client.BaseAddress + "polyanets"),
    Method = HttpMethod.Delete
});

WriteLine("--> Finishing X Shape creation process...");

public record struct Coordinate(int row, int column, Guid candidateId);



//const int megaverseSize = 10;
//Guid candidateId = Guid.Parse("d8f410a9-e5e4-492a-8a32-e2faeaffa9bb");

//using var client = new HttpClient { BaseAddress = new Uri("https://challenge.crossmint.io/api/") };

//var response = await client.GetAsync($"map/{candidateId}/goal");
//var json = await response.Content.ReadAsStringAsync();
//var goalMatrix = JsonSerializer.Deserialize<ApiGoalResponse>(json, options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

//WriteLine("--> Finishing Logo creation process..."); 
//public class ApiGoalResponse
//{
//    public string[][] Goal { get; set; }
//}


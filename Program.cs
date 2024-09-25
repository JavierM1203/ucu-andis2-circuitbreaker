using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly.CircuitBreaker;

class Program
{
    static async Task Main(string[] args)
    {
        var httpClient = new HttpClient();
        var apiClient = new ApiClient(httpClient);

        for (int i = 0; i < 10; i++)
        {
            try
            {
                var data = await apiClient.GetDataAsync("https://jsonplaceholder.typicode.com/posts/1");
                Console.WriteLine($"Datos recibidos: {data.Content.ReadAsStringAsync().Result}...");
            }
            catch (BrokenCircuitException)
            {
                Console.WriteLine("Circuito abierto. Llamada no realizada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener datos: {ex.Message}");
            }

            await Task.Delay(1000);
        }
    }
}
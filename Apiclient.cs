using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Wrap;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AsyncPolicyWrap<HttpResponseMessage> wrappedPolicy;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;

        var _circuitBreakerPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking: 2,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (result, breakDelay) =>
                        {
                            Console.WriteLine($"Circuito abierto durante {breakDelay.TotalSeconds} segundos debido a: {result.Exception?.Message ?? result.Result.ReasonPhrase}");
                        },
                     onReset: () => Console.WriteLine("Circuito cerrado. Operaciones normales resumidas."),
                     onHalfOpen: () => Console.WriteLine("Circuito en estado Half-Open. Probando la siguiente llamada."));


        var retryPolicy = Policy
              .Handle<HttpRequestException>()
              .OrResult<HttpResponseMessage>(x => !x.IsSuccessStatusCode)
              .RetryAsync(3, onRetry: (result, retryCount) =>
              {
                  Console.WriteLine($"Reintento {retryCount}");

              });




        /*    var fallbackPolicy = Policy<string>
         .Handle<Exception>()

         .FallbackAsync(fallbackValue: "Datos predeterminados", onFallbackAsync: ex =>
         {
             Console.WriteLine("Fallback activado.");
             return Task.CompletedTask;
         });
    */
        wrappedPolicy = Policy.WrapAsync(_circuitBreakerPolicy, retryPolicy);
    }




    public async Task<HttpResponseMessage> GetDataAsync(string url)
    {
        return await wrappedPolicy.ExecuteAsync(async () =>
        {
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Respuesta no exitosa: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            return response;
        });
    }
}
# ucu-andis2-circuitbreaker

**Implementación del Patrón Circuit Breaker en .NET**
---

### **Descripción:**

En este ejercicio, desarrollarás una aplicación en .NET que consume un servicio externo (API). Implementarás el patrón Circuit Breaker utilizando la librería **Polly** para manejar posibles fallos en la comunicación con el servicio externo. El objetivo es prevenir que las fallas en el servicio afecten negativamente a tu aplicación, mejorando así la resiliencia del sistema.

---

### **Objetivos Específicos:**

- **Comprender** cómo implementar el patrón Circuit Breaker en aplicaciones .NET.
- **Familiarizarse** con la librería Polly para manejo de resiliencia y tolerancia a fallos.
- **Configurar y Monitorear** el estado del Circuit Breaker durante la ejecución de la aplicación.
- **Analizar** el comportamiento del sistema ante diferentes escenarios de fallos.

---

### **Instrucciones Detalladas:**

#### **1. Preparación del Entorno**

- **Requisitos:**
  - .NET SDK 6.0 o superior instalado.
  - Un IDE como Visual Studio 2022 o Visual Studio Code.
- **Crear un Nuevo Proyecto:**
  - Abre tu IDE y crea un nuevo proyecto de tipo **Console App** o **ASP.NET Core Web API**.

#### **2. Simulación del Servicio Externo**

- **Opción A: Usar una API Pública**
  - Puedes utilizar una API pública como [JSONPlaceholder](https://jsonplaceholder.typicode.com/) para simular el servicio externo.
- **Opción B: Crear un Servicio Local**
  - Crea un proyecto adicional que actúe como servicio externo, donde puedas controlar y simular fallos.

#### **3. Añadir la Librería Polly**

- **Instalar Polly mediante NuGet:**

  ```bash
  dotnet add package Polly
  dotnet add package Microsoft.Extensions.Http.Polly
  ```

#### **4. Implementar el Cliente HTTP con Circuit Breaker**

- **Crear la Clase `ApiClient`:**

  ```csharp
  using System;
  using System.Net.Http;
  using System.Threading.Tasks;
  using Polly;
  using Polly.CircuitBreaker;

  public class ApiClient
  {
      private readonly HttpClient _httpClient;
      private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;

      public ApiClient(HttpClient httpClient)
      {
          _httpClient = httpClient;

          _circuitBreakerPolicy = Policy
              .Handle<HttpRequestException>()
              .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
              .CircuitBreakerAsync(
                  exceptionsAllowedBeforeBreaking: 2,
                  durationOfBreak: TimeSpan.FromSeconds(30),
                  onBreak: (result, breakDelay) =>
                  {
                      Console.WriteLine($"Circuito abierto durante {breakDelay.TotalSeconds} segundos debido a: {result.Exception?.Message ?? result.Result.ReasonPhrase}");
                  },
                  onReset: () => Console.WriteLine("Circuito cerrado. Operaciones normales resumidas."),
                  onHalfOpen: () => Console.WriteLine("Circuito en estado Half-Open. Probando la siguiente llamada.")
              );
      }

      public async Task<string> GetDataAsync(string url)
      {
          return await _circuitBreakerPolicy.ExecuteAsync(async () =>
          {
              var response = await _httpClient.GetAsync(url);

              if (!response.IsSuccessStatusCode)
              {
                  throw new HttpRequestException($"Respuesta no exitosa: {(int)response.StatusCode} {response.ReasonPhrase}");
              }

              return await response.Content.ReadAsStringAsync();
          });
      }
  }
  ```

- **Explicación de la Configuración:**
  - **exceptionsAllowedBeforeBreaking**: Número de excepciones antes de abrir el circuito.
  - **durationOfBreak**: Tiempo que el circuito permanecerá abierto antes de intentar restablecerse.
  - **onBreak**, **onReset**, **onHalfOpen**: Acciones a ejecutar en cada cambio de estado.

#### **5. Configurar el Programa Principal**

- **Modificar el `Main` para Consumir el `ApiClient`:**

  ```csharp
  using System;
  using System.Net.Http;
  using System.Threading.Tasks;

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
                  Console.WriteLine($"Datos recibidos: {data.Substring(0, 50)}...");
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
  ```

#### **6. Simular Fallos en el Servicio Externo**

- **Métodos para Simular Fallos:**
  - **Modificar la URL** para que apunte a un endpoint inexistente.
  - **Desconectar la red** temporalmente.
  - **Usar una herramienta** como Fiddler o Postman para interceptar y modificar las respuestas.

#### **7. Ejecutar y Observar el Comportamiento**

- **Escenario Normal:**
  - Ejecuta la aplicación sin fallos y observa que las solicitudes se realizan correctamente.
- **Escenario con Fallos:**
  - Introduce fallos y observa cómo el Circuit Breaker cambia de estado:
    - Después de dos fallos, el circuito se abre.
    - Durante el periodo abierto, las llamadas no se realizan.
    - Después del periodo, el circuito pasa a Half-Open y prueba una llamada.
    - Si la llamada es exitosa, el circuito se cierra; si falla, vuelve a abrirse.

#### **8. Extender la Funcionalidad**

- **Agregar Políticas de Reintento:**

  ```csharp
  using Polly.Retry;

  // Definir política de reintento
  var retryPolicy = Policy
      .Handle<HttpRequestException>()
      .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
      .RetryAsync(3, onRetry: (result, retryCount) =>
      {
          Console.WriteLine($"Reintento {retryCount} debido a: {result.Exception?.Message ?? result.Result.ReasonPhrase}");
      });

  // Combinar con Circuit Breaker
  var policyWrap = Policy.WrapAsync(_circuitBreakerPolicy, retryPolicy);
  ```

- **Implementar Fallbacks:**

  ```csharp
  using Polly.Fallback;

  var fallbackPolicy = Policy<string>
      .Handle<Exception>()
      .FallbackAsync(fallbackValue: "Datos predeterminados", onFallbackAsync: ex =>
      {
          Console.WriteLine("Fallback activado.");
          return Task.CompletedTask;
      });

  // Combinar todas las políticas
  var policyWrap = Policy.WrapAsync(fallbackPolicy, _circuitBreakerPolicy, retryPolicy);
  ```

#### **9. Documentar y Analizar**

- **Crear un Informe que Incluya:**
  - **Descripción de la Implementación:** Explica cómo configuraste el Circuit Breaker y otras políticas.
  - **Resultados de las Pruebas:** Incluye logs y capturas de pantalla.
  - **Análisis del Comportamiento:** Discute cómo el Circuit Breaker mejoró la resiliencia.
  - **Conclusiones y Mejoras Futuras:** Reflexiona sobre el ejercicio y posibles optimizaciones.

---

### **Entregables:**

1. **Código Fuente:**
   - Archivos `.cs` con la implementación completa.
2. **Informe Escrito:**
   - Documento en formato PDF o Word con el análisis solicitado.

---

### **Criterios de Evaluación:**

- **Funcionalidad Correcta (40%):** La aplicación debe ejecutar correctamente el patrón Circuit Breaker y manejar fallos como se espera.
- **Calidad del Código (20%):** Código limpio, organizado y bien comentado.
- **Análisis y Documentación (30%):** Informe detallado y reflexivo sobre la implementación y resultados.
- **Creatividad y Complejidad Adicional (10%):** Implementación de funcionalidades extra o análisis profundos.

---

### **Recursos Adicionales:**

- **Documentación de Polly:**
  - [Sitio Oficial de Polly](https://github.com/App-vNext/Polly)
  - [Wiki de Polly](https://github.com/App-vNext/Polly/wiki)
- **Tutoriales y Artículos:**
  - [Implementing the Circuit Breaker Pattern in .NET Core with Polly](https://www.pluralsight.com/guides/implementing-the-circuit-breaker-pattern-in-net-core-with-polly)
  - [Polly y Resiliencia en .NET](https://docs.microsoft.com/es-es/dotnet/architecture/cloud-native/resilient-applications/polly)
- **Videos Educativos:**
  - [Resiliencia con Polly en .NET (YouTube)](https://www.youtube.com/watch?v=E7Voso411Vs)
- **Libros Recomendados:**
  - *"Building Resilient Applications with Polly"* - [Autor]

---

### **Consejos para el Éxito:**

- **Comprende el Flujo de Estados:**
  - Asegúrate de entender cómo y cuándo el Circuit Breaker cambia de estado (Closed, Open, Half-Open).
- **Prueba Diferentes Escenarios:**
  - Simula no solo fallos, sino también recuperaciones exitosas para observar el restablecimiento del circuito.
- **Utiliza Logs Detallados:**
  - Implementa logging para facilitar el seguimiento y análisis del comportamiento de la aplicación.
- **Reflexiona sobre la Experiencia:**
  - Piensa en cómo este patrón podría aplicarse en proyectos reales y qué beneficios aportaría.

---

¡Buena suerte con el ejercicio! Esta práctica te brindará una comprensión sólida de cómo implementar y aprovechar el patrón Circuit Breaker en tus aplicaciones .NET, mejorando su resiliencia y confiabilidad.

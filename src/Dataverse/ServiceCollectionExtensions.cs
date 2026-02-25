using Mavrix.Common.Dataverse.AuthenticationTokenProvider;
using Mavrix.Common.Dataverse.Batch;
using Mavrix.Common.Dataverse.Clients;
using Mavrix.Common.Dataverse.DTO;
using Mavrix.Common.Dataverse.Options;
using Mavrix.Common.Dataverse.Repositories;
using Mavrix.Common.Dataverse.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;

namespace Mavrix.Common.Dataverse
{
	/// <summary>
	/// Extension methods for registering Dataverse clients, repositories, and related services.
	/// </summary>
	public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Registers the Dataverse HTTP client, token provider, serializer options, and retry policy.
		/// </summary>
		/// <param name="services">The service collection to add registrations to.</param>
		/// <param name="configuration">Configuration source containing Dataverse settings.</param>
		/// <param name="configureSerializer">Optional callback to customize serializer options.</param>
		/// <param name="useManagedIdentity">Whether to register the managed identity token provider.</param>
		/// <returns>The service collection for chaining.</returns>
		public static IServiceCollection AddDataverseClient(
			this IServiceCollection services, 
			ConfigurationManager configuration, 
			Action<JsonSerializerOptions>? configureSerializer = null,
			bool useManagedIdentity = true)
		{
			services.AddMemoryCache();
			services.AddOptions();

			if (useManagedIdentity)
			{
				services.TryAdd(ServiceDescriptor.Singleton<IAzureTokenProvider, ManagedIdentityTokenProvider>());
			}
			
			services.Configure<DataverseOptions>(configuration.GetSection(DataverseOptions.SectionName));

			services.AddSingleton(sp =>
			{
				var configurators = sp.GetServices<IDataverseJsonSerializerOptionsConfigurator>();
				return DataverseJsonSerializerOptionsFactory.Create(configureSerializer, configurators);
			});

			services.AddHttpClient<IDataverseHttpClient, DataverseHttpClient>()
				.AddTooManyRequestRetryHandler();

			services.TryAddSingleton<IDataverseBatchService, DataverseBatchService>();

			return services;
		}

		/// <summary>
		/// Adds default logging filters for Dataverse HTTP client noise reduction.
		/// </summary>
		/// <param name="builder">The logging builder to configure.</param>
		/// <returns>The logging builder for chaining.</returns>
		public static ILoggingBuilder AddDataverseDefaultLoggingSettings(this ILoggingBuilder builder)
		{
			builder.AddFilter("System.Net.Http.HttpClient.IDataverseHttpClient.LogicalHandler", LogLevel.Warning);
			builder.AddFilter("System.Net.Http.HttpClient.IDataverseHttpClient.ClientHandler", LogLevel.Warning);
			return builder;
		}

		/// <summary>
		/// Adds a retry handler for HTTP 429 (Too Many Requests) responses to the Dataverse client.
		/// </summary>
		/// <param name="builder">The HTTP client builder.</param>
		/// <returns>The resilience pipeline builder for further configuration.</returns>
		public static IHttpResiliencePipelineBuilder AddTooManyRequestRetryHandler(this IHttpClientBuilder builder)
		{
			return builder.AddResilienceHandler("default", static configure =>
			{
				configure.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>()
				{
					Delay = TimeSpan.FromSeconds(2),
					BackoffType = DelayBackoffType.Exponential,
					MaxRetryAttempts = 3,
					UseJitter = true,
					ShouldHandle = (response) => ValueTask.FromResult(response.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests),
					DelayGenerator = (response) => ValueTask.FromResult(response.Outcome.Result?.Headers?.RetryAfter?.Delta)
				});
			});
		}

		/// <summary>
		/// Registers a Dataverse repository for the specified entity type.
		/// </summary>
		/// <typeparam name="T">The DTO type representing the Dataverse set.</typeparam>
		/// <param name="services">The service collection to add registrations to.</param>
		/// <returns>The service collection for chaining.</returns>
		public static IServiceCollection AddDataverseRepository<T>(this IServiceCollection services) where T : DataverseTable
		{
			services.TryAdd(new ServiceDescriptor(typeof(IDataverseRepository<T>), sp =>
			{
				var client = sp.GetRequiredService<IDataverseHttpClient>();
				var options = sp.GetRequiredService<JsonSerializerOptions>();
				return new DataverseRepository<T>(client, options);
			}, ServiceLifetime.Singleton));
			return services;
		}

		/// <summary>
		/// Registers a JSON serializer options configurator for Dataverse serialization.
		/// </summary>
		/// <param name="services">The service collection to add registrations to.</param>
		/// <param name="configurator">Configurator instance to apply.</param>
		/// <returns>The service collection for chaining.</returns>
		public static IServiceCollection AddDataverseJsonSerializerConfigurator(this IServiceCollection services, IDataverseJsonSerializerOptionsConfigurator configurator)
		{
			services.AddSingleton(configurator);
			return services;
		}
	}
}

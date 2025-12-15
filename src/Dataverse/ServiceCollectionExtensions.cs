using Mavrix.Common.Dataverse.AuthenticationTokenProvider;
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
	public static class ServiceCollectionExtensions
	{
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

			return services;
		}

		public static ILoggingBuilder AddDataverseDefaultLoggingSettings(this ILoggingBuilder builder)
		{
			builder.AddFilter("System.Net.Http.HttpClient.IDataverseHttpClient.LogicalHandler", LogLevel.Warning);
			builder.AddFilter("System.Net.Http.HttpClient.IDataverseHttpClient.ClientHandler", LogLevel.Warning);
			return builder;
		}

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

		public static IServiceCollection AddDataverseJsonSerializerConfigurator(this IServiceCollection services, IDataverseJsonSerializerOptionsConfigurator configurator)
		{
			services.AddSingleton(configurator);
			return services;
		}
	}
}

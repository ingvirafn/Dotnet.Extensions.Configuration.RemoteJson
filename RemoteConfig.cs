using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Microsoft.Extensions.Configuration.RemoteJson
{
    public class RemoteConfigConfigurationSource : JsonStreamConfigurationSource
    {
        private readonly RemoteConfigOptions remoteConfigOptions;
        public RemoteConfigConfigurationSource(RemoteConfigOptions remoteConfigOptions) : base()
        {
            this.remoteConfigOptions = remoteConfigOptions;
        }

        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new RemoteConfigConfigurationProvider(this, this.remoteConfigOptions);
        }
    }

    public class RemoteConfigConfigurationProvider : Microsoft.Extensions.Configuration.Json.JsonStreamConfigurationProvider
    {

        private Task updateTsk;

        private object _lock = new object();
        private readonly RemoteConfigOptions remoteConfigOptions;
        private readonly HttpClient http;

        public RemoteConfigConfigurationProvider(
            RemoteConfigConfigurationSource source,
            RemoteConfigOptions remoteConfigOptions) : base(source)
        {
            this.remoteConfigOptions = remoteConfigOptions;
            this.http = new HttpClient();
            if (remoteConfigOptions.AuthorizationBearer != null)
            {
                http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {remoteConfigOptions.AuthorizationBearer}");
            }

            updateTsk = Task.Run(async () =>
            {
                while (true)
                {
                    await PollRemoteConfig();
                    await Task.Delay(remoteConfigOptions.PollingInterval);
                }
            });
        }

        private async Task PollRemoteConfig()
        {
            var jsonStream = await http.GetStreamAsync(remoteConfigOptions.Source);
            lock (_lock)
            {
                this.Data.Clear();
                this.Load(jsonStream);
            }
        }

        public override void Load()
        {
            lock (_lock)
            {
                // Lock, so we can wait for an update if it is ongoing
            }
        }

    }

    public static class RemoteConfigExtensions
    {
        public static IConfigurationBuilder AddRemoteConfig(
            this IConfigurationBuilder configurationBuilder,
            Action<RemoteConfigOptions> options)
        {
            _ = options ?? throw new ArgumentNullException(nameof(options));
            var configOptions = new RemoteConfigOptions();
            options(configOptions);
            return configurationBuilder.Add(new RemoteConfigConfigurationSource(configOptions));
        }

    }

    public class RemoteConfigOptions
    {
        public Uri Source { get; set; }
        public int PollingInterval { get; set; }
        public string AuthorizationBearer { get; set; }
    }

}

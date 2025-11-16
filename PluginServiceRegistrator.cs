using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Subtitles;
using Microsoft.Extensions.DependencyInjection;
using WizdomSubsDownloader.Providers;

namespace WizdomSubsDownloader;

/// <summary>
/// Registers services for the WizdomSubs plugin.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient("WizdomSubs", c =>
        {
            c.DefaultRequestHeaders.UserAgent.Clear();
            c.DefaultRequestHeaders.UserAgent.ParseAdd($"{applicationHost.Name}/{applicationHost.ApplicationVersionString}");
            c.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        });

        serviceCollection.AddSingleton<ISubtitleProvider, WizdomSubtitleProvider>();
    }
}

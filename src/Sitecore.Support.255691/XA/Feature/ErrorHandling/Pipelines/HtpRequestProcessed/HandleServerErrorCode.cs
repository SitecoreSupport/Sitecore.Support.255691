using System.IO;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sitecore.DependencyInjection;
using Sitecore.Diagnostics;
using Sitecore.IO;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.Web;
using Sitecore.XA.Foundation.Abstractions;
using Sitecore.XA.Foundation.Multisite;

namespace Sitecore.Support.XA.Feature.ErrorHandling.Pipelines.HtpRequestProcessed
{
  public class HandleServerErrorCode : HttpRequestProcessor
  {
    protected ISiteInfoResolver SiteInfoResolver;
    protected IContext Context { get; } = ServiceLocator.ServiceProvider.GetService<IContext>();

    public HandleServerErrorCode()
    {
      SiteInfoResolver = ServiceLocator.ServiceProvider.GetService<ISiteInfoResolver>();
    }

    public override void Process(HttpRequestArgs args)
    {
      if (args.Context.Error != null && !Context.Site.Name.Equals("shell"))
      {
        var siteInfos = GetPossibleSites();
        var site = SiteInfoResolver.ResolveSiteFromRequest(siteInfos, new HttpRequestWrapper(args.Context.Request));

        if (site != null)
        {
          var url = GetStaticErrorPageUrl(site);
          #region FIX 255691
          var configuration = WebConfigurationManager.OpenWebConfiguration("~/Web.config");

          CustomErrorsSection customErrors =
          (CustomErrorsSection)configuration.GetSection("system.web/customErrors");

          if (customErrors.Mode == CustomErrorsMode.On)
          {
            TransferRequestToServerErrorPage(args, site, url);
          }
          if (customErrors.Mode == CustomErrorsMode.RemoteOnly)
          {
            if (!WebUtil.IsLocalRequest(args.Context.Request))
            {
              TransferRequestToServerErrorPage(args, site, url);
            }
          }
          #endregion
        }
      }
    }

    private void TransferRequestToServerErrorPage(HttpRequestArgs args, SiteInfo site, string url)
    {
      if (File.Exists(FileUtil.MapPath(url)))
      {
        args.Context.Server.TransferRequest(url);
      }
      else
      {
        Log.Warn($"Could not find proper static error page for site: {site.Name}. Please generate it.", this);
      }
    }

    protected virtual SiteInfo[] GetPossibleSites()
    {
      return SiteInfoResolver.Sites.OrderByDescending(info => info.VirtualFolder.Length).ToArray();
    }

    protected virtual string GetStaticErrorPageUrl(SiteInfo site)
    {
      return string.Format("{0}/{1}.html", "/ErrorPages", site.Name);
    }
  }
}
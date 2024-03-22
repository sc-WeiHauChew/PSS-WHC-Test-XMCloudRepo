namespace Sitecore.Support.XA.Foundation.SiteMetadata.Pipelines.HttpRequestBegin
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Runtime.Remoting.Contexts;
    using System.Text;
    using System.Web;
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.Data.Fields;
    using Sitecore.Data.Items;
    using Sitecore.DependencyInjection;
    using Sitecore.Diagnostics;
    using Sitecore.Links;
    using Sitecore.Links.UrlBuilders;
    using Sitecore.Pipelines.HttpRequest;
    using Sitecore.SecurityModel;
    using Sitecore.Sites;
    using Sitecore.Web;
    using Sitecore.XA.Foundation.Abstractions;
    using Sitecore.XA.Foundation.Abstractions.Configuration;
    using Sitecore.XA.Foundation.Multisite;
    using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
    using Sitecore.XA.Foundation.SitecoreExtensions.Utils;
    using Sitecore.XA.Foundation.SiteMetadata;
    using Sitecore.XA.Foundation.SiteMetadata.Enums;
    using Sitecore.XA.Foundation.SiteMetadata.Models.Sitemap;
    using Sitecore.XA.Foundation.SiteMetadata.Services;
    using Sitecore.XA.Foundation.SiteMetadata.Settings;
    using Sitecore.XA.Foundation.SiteMetadata.Sitemap;

    public class SxaSitemapHandler : Sitecore.XA.Foundation.SiteMetadata.Pipelines.HttpRequestBegin.SxaSitemapHandler
    {
        public override void Process(HttpRequestArgs args)
        {
            Uri url = HttpContext.Current.Request.Url;
            bool flag = url.PathAndQuery.EndsWith("/sitemap.xml", StringComparison.OrdinalIgnoreCase);
            string fileName = Path.GetFileName(url.PathAndQuery);
            bool flag2 = fileName != null && fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && fileName.StartsWith("sitemap-", StringComparison.OrdinalIgnoreCase);
            if (!(flag || flag2))
            {
                return;
            }
            if (CurrentSite == null || !UrlUtils.IsUrlValidForFile(url, CurrentSite, "/" + Path.GetFileName(url.PathAndQuery), StringComparison.OrdinalIgnoreCase))
            {
                Log.Info("SitemapHandler (sitemap.xml) : " + $"cannot resolve site or url ({url})", this);
                return;
            }
            SitemapSettings sitemapSettings = GetSitemapSettings();
            if (sitemapSettings == null)
            {
                Log.Info("SitemapHandler (sitemap.xml) : missing sitemap settings item", this);
                return;
            }
            if (sitemapSettings.CacheType == SitemapStatus.Inactive)
            {
                Log.Info("SitemapHandler (sitemap.xml) : " + $"sitemap is off (status : {sitemapSettings.CacheType})", this);
                return;
            }
            SitemapContent sitemap = SitemapManager.GetSitemap(Context.Site);

            var moreSiteNames = SiteContextFactory.GetSiteNames().Where(name => !name.Equals(Context.Site.Name));// Customization start get all site names
            List<SiteContext> liSiteContext = new List<SiteContext>();
            foreach (string siteName in moreSiteNames)
            {
                liSiteContext.Add(SiteContextFactory.GetSiteContext(siteName));// Get SiteContext to generate sitemap with
            }
            foreach (SiteContext sc in liSiteContext)
            {
                sitemap.Values.AddRange(SitemapManager.GetSitemap(sc).Values);// Merge with current site sitemap
            }

            if (sitemap == null)
            {
                return;
            }
            if (flag)
            {
                Item settingsItem = GetSettingsItem();
                CheckboxField checkboxField = settingsItem.Fields[Sitecore.XA.Foundation.SiteMetadata.Templates.Sitemap._SitemapSettings.Fields.SitemapIndex];
                if (sitemap.Values.Count == 1 && !checkboxField.Checked)
                {
                    SetResponse(args.HttpContext.Response, sitemap.Values.FirstOrDefault());
                }
                else
                {
                    NameValueCollection nameValueCollection = new NameValueCollection();
                    if (checkboxField.Checked)
                    {
                        nameValueCollection.Merge(GetExternalSitemaps(settingsItem));
                    }
                    int num = 1;
                    string indexUrlPrefix = GetIndexUrlPrefix(settingsItem);
                    foreach (string value2 in sitemap.Values)
                    {
                        _ = value2;
                        string value = $"{indexUrlPrefix}/sitemap-{num++}.xml";
                        nameValueCollection.Add($"{Guid.NewGuid()}", value);
                    }
                    ISitemapGenerator service = ServiceLocator.ServiceProvider.GetService<ISitemapGenerator>();
                    SetResponse(args.HttpContext.Response, service.BuildSitemapIndex(nameValueCollection));
                }
            }
            else
            {
                if (!int.TryParse(Path.GetFileNameWithoutExtension(url.PathAndQuery).Replace("sitemap-", string.Empty), out var result) || sitemap.Values.Count < result)
                {
                    return;
                }
                SetResponse(args.HttpContext.Response, sitemap.Values[result - 1]);
            }
            args.AbortPipeline();
        }
    }
}
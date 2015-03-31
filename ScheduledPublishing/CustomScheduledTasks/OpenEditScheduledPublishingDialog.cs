﻿using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Text;
using Sitecore.Web.UI.Sheer;
using System.Collections.Specialized;
using Version = Sitecore.Data.Version;

namespace ScheduledPublishing.CustomScheduledTasks
{
    public class OpenEditScheduledPublishingDialog : Command
    {
        public override void Execute(CommandContext context)
        {
            Assert.ArgumentNotNull((object)context, "context");
            if (context.Items.Length != 1)
                return;
            this.Execute(context.Items[0]);
        }

        public void Execute(Item item)
        {
            Assert.ArgumentNotNull((object)item, "item");
            NameValueCollection parameters = new NameValueCollection();
            parameters["id"] = item.ID.ToString();
            parameters["language"] = item.Language.ToString();
            parameters["version"] = item.Version.ToString();
            parameters["databasename"] = item.Database.Name;
            Context.ClientPage.Start((object)this, "Run", parameters);
        }

        protected void Run(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull((object)args, "args");
            string dbName = args.Parameters["databasename"];
            string id = args.Parameters["id"];
            string lang = args.Parameters["language"];
            string ver = args.Parameters["version"];
            Database database = Factory.GetDatabase(dbName);
            Assert.IsNotNull((object)database, dbName);
            Item obj = database.Items[id, Language.Parse(lang), Version.Parse(ver)];
            if (obj == null)
            {
                SheerResponse.Alert("Item not found.");
            }
            else
            {
                if (!SheerResponse.CheckModified())
                    return;
                if (args.IsPostBack)
                {
                    return;
                }
                UrlString urlString = new UrlString(UIUtil.GetUri("control:EditScheduledPublishing"));
                urlString.Append("id", obj.ID.ToString());
                SheerResponse.ShowModalDialog(urlString.ToString(), "500", "300", string.Empty, true);
                args.WaitForPostBack();
            }
        }
    }
}
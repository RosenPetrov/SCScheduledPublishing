﻿using System.Collections.Generic;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Publishing;
using Sitecore.Tasks;
using System;
using System.Linq;
using System.Net.Mail;

namespace ScheduledPublishing.CustomScheduledTasks
{
    /// <summary>
    /// Publishes the item(s) passed
    /// </summary>
    public class ScheduledPublishingCommand
    {
        protected Database Master;
        private const string PUBLISHING_SCHEDULES_PATH = "/sitecore/system/Tasks/PublishingSchedules";

        public void SchedulePublishingTask(Item[] itemArray, CommandItem commandItem, ScheduleItem scheduledItem)
        {
            Master = Sitecore.Configuration.Factory.GetDatabase("master");
            List<Item> itemsToPublish = GetItemsToPublish();

            foreach (var item in itemsToPublish)
            {
                Log.Info("Custom scheduled for publish (2): " + item.Name, this);
                if (scheduledItem != null)
                {
                    bool isUnpublish = item["Unpublish"] == 1.ToString();
                    //// if the item has Publishing targets defined, use them and publish to all of them
                    //if (!string.IsNullOrEmpty(item[FieldIDs.PublishingTargets]))
                    //{
                    //    publishingTargets = item[FieldIDs.PublishingTargets].Split('|');
                    //}
                    //// if the item has no Publishing targets specified, publish to all
                    //else
                    //{
                    List<string> publishingTargets =
                        Master.GetItem("/sitecore/system/Publishing targets")
                            .Children.Select(x => x.ID.ToString())
                            .ToList();
                    //}
                    if (publishingTargets.Count == 0)
                    {
                        Log.Info("No publishing targets found", this);
                    }
                    else
                    {
                        bool isSuccessful = PublishItemToTargets(item, publishingTargets, isUnpublish);
                        Notify("PNGPublishing@png.com", item["CreatedByEmail"], isUnpublish, item, isSuccessful);
                    }
                }
                else Log.Info("scheduled item null", this);
            }
        }

        private List<Item> GetItemsToPublish()
        {
            try
            {
                Item schedulesFolder = Context.ContentDatabase.GetItem(PUBLISHING_SCHEDULES_PATH);
                List<Item> itemsToPublish = new List<Item>();
                foreach (Item schedule in schedulesFolder.Children)
                {
                    if (!string.IsNullOrEmpty(schedule["Schedule"]) && !string.IsNullOrEmpty(schedule["Items"]))
                    {
                        DateTime targetDate = DateUtil.IsoDateToDateTime(schedule["Schedule"].Split('|').First());
                        if (DateTime.Compare(targetDate.AddHours(1), DateTime.Now) <= 0)
                        {
                            Item targetItem = Context.ContentDatabase.GetItem(schedule["Items"]);
                            itemsToPublish.Add(targetItem);
                        }
                    }
                }

                return itemsToPublish;
            }
            catch (Exception e)
            {
                Log.Info(e.ToString(), this);
            }

            return new List<Item>();
        }

        private bool PublishItemToTargets(Item item, IEnumerable<string> publishingTargets, bool isUnpublish)
        {
            bool successful = false;
            foreach (var pbTargetId in publishingTargets)
            {
                try
                {
                    if (isUnpublish)
                    {
                        item.Editing.BeginEdit();
                        item["__Never publish"] = 1.ToString();
                        item.Editing.AcceptChanges();
                        item.Editing.EndEdit();
                    }

                    Item pbTarget = Master.GetItem(new ID(pbTargetId));
                    PublishOptions publishOptions = new PublishOptions(
                        Master,
                        Database.GetDatabase(pbTarget["Target database"]),
                        PublishMode.SingleItem,
                        item.Language,
                        DateTime.Now);
                    Sitecore.Publishing.Pipelines.PublishItem.PublishItemPipeline.Run(item.ID, publishOptions);

                    Log.Info(
                        "Scheduled publishing task complete for " + item.Name + " - " + item.ID
                        + " Database source: " + Master.Name + " Database target: " +
                        Database.GetDatabase(pbTarget["Target database"]).Name, this);

                    if (isUnpublish)
                    {
                        item.Editing.BeginEdit();
                        item["__Never publish"] = string.Empty;
                        item.Editing.AcceptChanges();
                        item.Editing.EndEdit();
                    }

                    successful = true;
                }
                catch (Exception e)
                {
                    Log.Info("Scheduled publishing task failed for " + item.Name + " - " + item.ID, this);
                    Log.Info(e.ToString(), this);
                    successful = false;
                }
            }

            return successful;
        }

        public void Notify(string emailFrom, string emailTo, bool isUnpublish, Item item, bool success)
        {
            string action = isUnpublish ? "Unpublishing" : "Publishing";
            string body = success
                ? action + " {0} ({1}) completed successfully at {2}."
                : action + " {0} ({1}) failed at {2}. Please restart publishing process.";

            var smtpClient = new SmtpClient();
            var mailMessage = new MailMessage(emailFrom, emailTo)
            {
                Subject = "PNG Publishing",
                IsBodyHtml = true,
                Body = string.Format(body, item.Name, item.Paths.FullPath, DateTime.Now),
            };

            try
            {
                smtpClient.Send(mailMessage);
            }
            catch (Exception e)
            {
                Log.Info("Sending email failed: " + e.ToString(), this);
            }
        }
    }
}
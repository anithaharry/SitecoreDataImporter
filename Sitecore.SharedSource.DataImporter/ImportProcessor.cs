﻿using Sitecore.Data.Items;
using Sitecore.SharedSource.DataImporter.Extensions;
using Sitecore.SharedSource.DataImporter.Logger;
using Sitecore.SharedSource.DataImporter.Providers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Sitecore.Data;
using Sitecore.Jobs;
using Sitecore.SharedSource.DataImporter.Mappings.Processors;
using Sitecore.SharedSource.DataImporter.Utility;

namespace Sitecore.SharedSource.DataImporter
{
	public class ImportProcessor
	{

		protected ILogger Logger { get; set; }

		protected IDataMap DataMap { get; set; }

		public ImportProcessor(IDataMap dm, ILogger l)
		{
			if (dm == null)
				throw new ArgumentNullException("The provided Data Map was null");
			if (l == null)
				throw new ArgumentNullException("The provided Logger was null");

			DataMap = dm;
			Logger = l;
		}

		/// <summary>
		/// processes each field against the data provided by subclasses
		/// </summary>
		public void Process()
		{
			Logger.Log("N/A", string.Format("Import Started at: {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            
            JobUtil.SetJobPriority(ThreadPriority.Highest);

			IEnumerable<object> importItems;
			try
			{
				importItems = DataMap.GetImportData();
			}
			catch (Exception ex)
			{
				Logger.Log("N/A", string.Format("GetImportData Failed: {0}", ex.Message), ProcessStatus.Error);
				Logger.Log("N/A", string.Format("Import Finished at: {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

                JobUtil.SetJobState(JobState.Finished);

				return;
			}

			int totalLines = importItems.Count();
            JobUtil.SetJobTotal(totalLines);

			long line = 0;

			using (new BulkUpdateContext())
			{ // try to eliminate some of the extra pipeline work
				foreach (object importRow in importItems)
				{
					var sw = new Stopwatch();
					sw.Start();
					//import each row of data
					line++;
					try
					{
						string newItemName = DataMap.BuildNewItemName(importRow);
						if (string.IsNullOrEmpty(newItemName))
						{
							Logger.Log("N/A", string.Format("BuildNewItemName failed on import row {0} because the new item name was empty", line), ProcessStatus.NewItemError);
							continue;
						}

						Item thisParent = DataMap.GetParentNode(importRow, newItemName);
						if (thisParent.IsNull())
						{
							Logger.Log("N/A", string.Format("Get parent failed on import row {0} because the new item's parent is null", line), ProcessStatus.NewItemError);
							continue;
						}

						var newItem = DataMap.CreateNewItem(thisParent, importRow, newItemName);
                        
					}
					catch (Exception ex)
					{
						var rows = importRow as Dictionary<string, string>;
						var values = rows == null ? string.Empty : string.Join("||", (rows).Select(a => $"{a.Key}-{a.Value}"));
						Logger.Log("N/A", string.Format("Exception thrown on import row {0} : {1}", line, ex.Message), ProcessStatus.NewItemError, "All Import Values", values);
					}
					sw.Stop();
					Logger.Log("Performance Statistic", $"Used {sw.Elapsed.TotalSeconds} to process this item.");

                    JobUtil.SetJobStatus(line);
				}

			    var currentPostProcessor = 1;
                JobUtil.SetJobTotal(DataMap.PostProcessors.Count);
                foreach (IPostProcessor p in DataMap.PostProcessors)
			    {
                    JobUtil.SetJobStatus(currentPostProcessor);
                    
                    try
			        {
                        p.Process(DataMap);
			        }
			        catch (Exception ex)
			        {
			            Logger.Log(string.Format("Post Processor of type: {0} failed. Error: {1}", p.GetType(), ex), "N/A", ProcessStatus.PostProcessorError, "N/A", "N/A");
                    }

			        currentPostProcessor++;
			    }
			}
            
			//if no messages then you're good
			if (!Logger.LoggedError)
				Logger.Log("Success", "the import completed successfully");

			Logger.Log("N/A", string.Format("Total Items Processed: {0}", totalLines));
			Logger.Log("N/A", string.Format("Import Finished at: {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

            JobUtil.SetJobState(JobState.Finished);
		}
	}
}

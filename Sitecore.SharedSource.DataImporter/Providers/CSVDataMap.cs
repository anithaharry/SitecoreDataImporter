﻿using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Resources.Media;
using Sitecore.SharedSource.DataImporter.Extensions;
using Sitecore.SharedSource.DataImporter.Logger;
using Sitecore.SharedSource.DataImporter.Mappings.Fields;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sitecore.SharedSource.DataImporter.Providers
{
    public class CSVDataMap : BaseDataMap {

		#region Properties

		public string FieldDelimiter { get; set; }

		public string EncodingType { get; set; }

		#endregion Properties

        #region Constructor

		public CSVDataMap(Database db, string ConnectionString, Item importItem, ILogger l)
            : base(db, ConnectionString, importItem, l) {

			FieldDelimiter = ImportItem.GetItemField("Field Delimiter", Logger);
            EncodingType = ImportItem.GetItemField("Encoding Type", Logger);
		}

        #endregion Constructor

        #region IDataMap Methods

        /// <summary>
		/// uses the query field to retrieve file data
		/// </summary>
		/// <returns></returns>
        public override IEnumerable<object> GetImportData() {

            var isSitecorePath = Query.StartsWith("/sitecore/");

            if (!isSitecorePath && !File.Exists(this.Query))
            {
                Logger.Log("N/A", string.Format("the file: '{0}' could not be found. Try moving the file under the webroot.", this.Query), ProcessStatus.Error);
                return Enumerable.Empty<object>();
            }

            string data = (isSitecorePath) ? GetContentString(Query) : GetFileString(Query);

            //split urls by breaklines
            List<string> lines = SplitString(data, "\n");

            return lines;
        }
		
		/// <summary>
		/// There is no custom data for this type
		/// </summary>
		/// <param name="newItem"></param>
		/// <param name="importRow"></param>
        public override void ProcessCustomData(ref Item newItem, object importRow) { }

		/// <summary>
		/// gets a field value from an item
		/// </summary>
		/// <param name="importRow"></param>
		/// <param name="fieldName"></param>
		/// <returns></returns>
        public override string GetFieldValue(object importRow, string fieldName) {
			
			string item = importRow as string;
			List<string> cols = SplitColumns(item, FieldDelimiter);
			
			int pos = -1;
			string s = string.Empty;
			if(int.TryParse(fieldName, out pos) && (cols[pos] != null))
				s = cols[pos];
			return s;
		}

        #endregion IDataMap Methods

        #region Methods

        protected List<string> SplitString(string str, string splitter){
            // string split options set to none so that empty columns are allowed
            // useful for importing large csv files, so you don't have to check the content
			return str.Split(new string[] { splitter }, StringSplitOptions.None).ToList();
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str">"col1","col2","col2"</param>
        /// <param name="splitter">,</param>
        /// <returns>new List<string> { "col1", "col2", "col3" }</string></returns>
        protected List<string> SplitColumns(string str, string splitter)
        {
            char delimitter = splitter[0];
            List<string> stringValues = new List<string>();
            stringValues = str.Split(delimitter).ToList<string>();
            return stringValues;
        }

        protected string GetContentString(string contentPath)
        {
            var fileItem = (MediaItem)ToDB.GetItem(Query);
            if (fileItem == null)
                return string.Empty;

            using (var reader = new StreamReader(MediaManager.GetMedia(fileItem).GetStream().Stream))
            {
                string text = reader.ReadToEnd();

                return text;
            }
        }

        protected string GetFileString(string filePath)
        {
            //open the file selected
            FileInfo f = new FileInfo(filePath);
            FileStream s = f.OpenRead();
            byte[] bytes = new byte[s.Length];
            s.Position = 0;
            int currentBytesRead = 0;
            int totalBytesRead = 0;
            while (s.Read(bytes, 0, int.Parse(s.Length.ToString())) > 0)
            {
                totalBytesRead += currentBytesRead;
            }

            Encoding et = Encoding.GetEncoding("utf-8");
            int ei = -1;
            if (!EncodingType.Equals(""))
            {
                et = (int.TryParse(EncodingType, out ei))
                    ? Encoding.GetEncoding(ei)
                    : Encoding.GetEncoding(EncodingType);
            }

            return et.GetString(bytes);
        }

        private void ProcessFields(object importRow, Item newItem)
        {
            //add in the field mappings
            List<IBaseField> fieldDefs = GetFieldDefinitionsByRow(importRow);
            ProcessFields(importRow, newItem, fieldDefs);
        }
        
        private void ProcessFields(object importRow, Item newItem, List<IBaseField> fieldDefs)
        {
            //add in the field mappings
            foreach (IBaseField d in fieldDefs)
            {
                string importValue = string.Empty;
                try
                {
                    IEnumerable<string> values = GetFieldValues(d.GetExistingFieldNames(), importRow);
                    importValue = String.Join(d.GetFieldValueDelimiter(), values);
                    d.FillField(this, ref newItem, importValue);
                }
                catch (Exception ex)
                {
                    Logger.Log("Process Fields", string.Format("the FillField failed for field {1} on item {0}", newItem.Paths.FullPath, d.Name));
                }
            }
        }

        #endregion Methods
    }
}

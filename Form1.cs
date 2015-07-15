using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.Entity;
using System.IO;

namespace tx_sort_query
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private string sDataSource = "data.xml";
        private string sTemplateFile = "template.tx";

        private void mergeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // convert the XML file to a .NET DataSet
            DataSet ds = new DataSet();
            ds.ReadXml(sDataSource, XmlReadMode.Auto);

            // create a new DataSelector instance
            DataSelector selector = new DataSelector(ds, File.ReadAllBytes(sTemplateFile));

            // load the modified template
            mailMerge1.LoadTemplateFromMemory(selector.Template, TXTextControl.DocumentServer.FileFormat.InternalUnicodeFormat);
            // merge the template with the new, sorted data source
            mailMerge1.Merge(ds.Tables[0]);

            // copy the merged document into a visible TextControl object
            byte[] data = null;
            mailMerge1.SaveDocumentToMemory(out data, TXTextControl.BinaryStreamType.InternalUnicodeFormat, null);
            textControl1.Load(data, TXTextControl.BinaryStreamType.InternalUnicodeFormat);
        }
    }

    // DataSelector
    // description: This class loops through all merge blocks in a given template to check
    // for sort keywords. The given referenced DataSet will be sorted.
    //
    // Parameters: dataSet of type DataSet, template as a byte[] array in the InternalUnicodeFormat
    public class DataSelector
    {
        public byte[] Template { get; set; }

        public DataSelector(DataSet dataSet, byte[] template)
        {
            // use a temporary ServerTextControl instance to recognize blocks with
            // the sorting keyword 'ORDERBY'
            using (TXTextControl.ServerTextControl tx = new TXTextControl.ServerTextControl())
            {
                tx.Create();
                // load the template
                tx.Load(template, TXTextControl.BinaryStreamType.InternalUnicodeFormat);

                // create a list of merge blocks
                List<MergeBlock> lMergeBlocks = MergeBlock.GetMergeBlocks(MergeBlock.GetBlockMarkersOrdered(tx), tx);

                // loop through all merge blocks to check whether they
                // have a sorting parameter
                foreach (MergeBlock mergeBlock in lMergeBlocks)
                {
                    string sBlockName = mergeBlock.StartMarker.TargetName;

                    // check for the unique sorting parameter
                    if (sBlockName.ToUpper().Contains("ORDERBY") == false)
                        continue;

                    // create a new SortedBlock object to store parameter
                    SortedBlock block = new SortedBlock(sBlockName);

                    // remove the sorting parameter from the block name
                    // so that MailMerge can find the matching data in the data source
                    mergeBlock.StartMarker.TargetName = block.Name;
                    mergeBlock.EndMarker.TargetName = "BlockEnd_" + mergeBlock.Name;

                    if (dataSet.Tables.Contains(mergeBlock.Name) == false)
                        continue;

                    // get all DataRows using LINQ
                    var query = from product in dataSet.Tables[mergeBlock.Name].AsEnumerable()
                                select product;

                    // create a new DataTable with the sorted DataRows
                    DataTable dtBoundTable = (block.SortType == SortType.ASC) ?
                        query.OrderBy(product => product.Field<String>(block.ColumnName)).CopyToDataTable() :
                        query.OrderByDescending(product => product.Field<String>(block.ColumnName)).CopyToDataTable();

                    // remove original rows and replace with sorted rows
                    dataSet.Tables[mergeBlock.Name].Rows.Clear();
                    dataSet.Tables[mergeBlock.Name].Merge(dtBoundTable);

                    dtBoundTable.Dispose();
                }

                // save the template
                byte[] data = null;
                tx.Save(out data, TXTextControl.BinaryStreamType.InternalUnicodeFormat);
                this.Template = data;
            }
        }
    }

    // SortedBlock
    // description: this class is a container to store the block sorting parameters
    // the parameter string has the following format:
    //
    // [BLOCKNAME];orderby,[COLUMNNAME],DESC|ASC
    public class SortedBlock
    {
        public string Name { get; set; }
        public string ColumnName { get; set; }
        public SortType SortType { get; set; }

        public SortedBlock(string SortedBlockName)
        {
            // the name of the block is the first part the semicolon separated string
            string[] saBlockValues = SortedBlockName.Split(';');
            this.Name = (string)saBlockValues.GetValue(0);
            
            // the second part of separated by commas
            string[] saSortedBlockParameters = 
                ((string)saBlockValues.GetValue(1)).Split(',');
            this.ColumnName = saSortedBlockParameters[1];
            
            this.SortType = 
                (SortType)Enum.Parse(typeof(SortType), 
                saSortedBlockParameters[2]);
        }
    }

    public enum SortType
    {
        ASC = 0,
        DESC = 1
    }
}

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Args;
using XrmToolBox.Extensibility.Interfaces;

namespace XrmToolBox___Bulk_Delete_Tool
{
    public partial class BulkDeleteTool : PluginControlBase, IGitHubPlugin, IPayPalPlugin, IStatusBarMessenger, IMessageBusHost
    {
        /// <summary>
        /// TODO: Update GitHub information
        /// TODO: Update Help information
        /// TODO: Run for first X records
        /// TODO: Allow Selection of Multiple Views?
        /// </summary>

        #region Custom Variables

        public event EventHandler<MessageBusEventArgs> OnOutgoingMessage;
        public event EventHandler<StatusBarMessageEventArgs> SendMessageToStatusBar;
        private EntityMetadataCollection allEntityMetadata = new EntityMetadataCollection();
        private EntityCollection views = new EntityCollection();
        private EntityCollection ExecutionRecordSet = new EntityCollection();
        private ExecuteMultipleRequest requestWithResults = new ExecuteMultipleRequest();
        private Dictionary<string, string> EntityNameMap = new Dictionary<string, string>();
        private int emrCount = 0;
        public int errorCount = 0;
        public int emrBatchSize = 200;
        public List<TimeSpan> _listTS = new List<TimeSpan>();
        TimeSpan avgTS = new TimeSpan();
        TimeSpan estTS = new TimeSpan();

        #endregion Custom Variables

        public BulkDeleteTool()
        {
            InitializeComponent();
        }

        private void BulkDeleteTool_Load_1(object sender, EventArgs e)
        {
            ExecuteMethod(loadEntities);
        }            

        #region Tool Strip Methods

        private void tsbClose_Click(object sender, EventArgs e)
        {
            CloseTool();
        }
        private void toolStripSplitButton1_ButtonClick(object sender, EventArgs e)
        {

        }
        private void toolStripDropDownButton1_Click(object sender, EventArgs e)
        {
            //ExecuteMethod(loadEntities);
        }
        private void entitiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExecuteMethod(loadEntities);
        }
        private void viewsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExecuteMethod(loadViews);
        }
        private void tsbCount_Click(object sender, EventArgs e)
        {
            ExecuteMethod(getRecordCount);
        }
        private void tsbExecute_Click(object sender, EventArgs e)
        {
            ExecuteMethod(ProcessDeletes);
        }
        private void tsbtnStop_Click(object sender, EventArgs e)
        {
            CancelWorker();
        }
        #endregion Tool Strip Methods

        #region Form Methods
        private void radFetchXML_CheckedChanged(object sender, EventArgs e)
        {
            if (radFetchXML.Checked)
            {
                rtxtFetchXML.Enabled = toolStripSeparator5.Visible = tsbtnFetchBuilder.Visible = tsbtnFetchBuilder.Enabled = true;
            }

        }

        private void radViews_CheckedChanged(object sender, EventArgs e)
        {
            if (radViews.Checked)
            {
                rtxtFetchXML.Enabled = toolStripSeparator5.Visible = tsbtnFetchBuilder.Visible = tsbtnFetchBuilder.Enabled = false;
            }
        }

        private void cmbEntities_SelectedIndexChanged(object sender, EventArgs e)
        {
            ExecuteMethod(loadViews);
        }

        private void lstViews_SelectedIndexChanged(object sender, EventArgs e)
        {
            int _selectedViewIndex = 0;
            ExecutionRecordSet.Entities.Clear();

            string _fetchXML = "";

            _selectedViewIndex = lstViews.SelectedIndex;
            txtRecordCount.Clear();

            if (_selectedViewIndex == -1) { return; }
            try
            {
                _fetchXML = (String)views[_selectedViewIndex]["fetchxml"];

                XDocument XDocument = XDocument.Parse(_fetchXML);

                rtxtFetchXML.Text = XDocument.ToString();
                tsbCount.Enabled = true;
            }
            catch (Exception)
            {

                //throw;
            }

        }

        private void rtxtFetchXML_TextChanged(object sender, EventArgs e)
        {
            ExecutionRecordSet.Entities.Clear();
            txtRecordCount.Clear();

            tsbCount.Enabled = true;
            tsbExecute.Enabled = false;
            //tsbCancel.Enabled = false;
        }
        private void txtBatchSize_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtBatchSize.Text))
            {
                bool batchIsNumeric = int.TryParse(txtBatchSize.Text, out int n);
                if (!batchIsNumeric || n > 1000 || n == 0) { MessageBox.Show("Please enter a number value (1-1000) into Batch Size to represent the amount of deletes in each batch execution. 200 is recommended.", "Bulk Delete Tool"); }
            }
        }
        private void txtInterval_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtInterval.Text))
            {
                bool batchIsNumeric = int.TryParse(txtInterval.Text, out int n);
                if (!batchIsNumeric) { MessageBox.Show("Please enter a number value into Interval to represent the number of seconds between each batches execution.", "Bulk Delete Tool"); } 
            }
        }
        private void tsbtnFetchBuilder_Click(object sender, EventArgs e)
        {
            OnOutgoingMessage(this, new MessageBusEventArgs("FetchXML Builder")
            {
                TargetArgument = rtxtFetchXML.Text
            });
        }
        private void tsbtnHelp_Click(object sender, EventArgs e)
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                rtxtFetchXML.Clear();
                rtxtFetchXML.AppendText("INSTRUCTIONS");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("----------------------------");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("1. Select an Entity from the drop down or click the Refresh drop down and then the Entities Button.");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("2. Select an existing view, customize an existing view, or create a fully custom view. When Custom FetchXML is selected, there will be an option to edit the query in FetchXML Builder.");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("4. Click 'Validate Query' to validate the FetchXML Query and get a record count.");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("5. If no errors with the Query, click 'Delete Records', sit back, relax, and enjoy!");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("INFORMATION");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("----------------------------");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("Batch Size: # of Records to Delete per Batch/CRM Web Service call. 200 is recommended.");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("Interval Delay: # of Seconds between Batches being sent to CRM");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("CONTACT INFO");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("----------------------------");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("Email: andrewopopkin@gmail.com");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("Twitter: @andypopkin");
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText(Environment.NewLine);
                rtxtFetchXML.AppendText("** Please contact me if you have any issues or ideas for this tool or other XrmToolBox based tools. Thank you for using this tool! If you really enjoy, click About > Donate > Bulk Delete Tool button :) **");
            });
        }
        #endregion Form methods

        #region Main Tool Methods

        private void loadEntities()
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Retrieving Entity Metadata...",
                Work = (w, e) =>
                {
                    #region Reset Variables

                    allEntityMetadata.Clear();// = new EntityMetadataCollection();
                    ExecutionRecordSet.Entities.Clear();
                    views.Entities.Clear();
                    ExecutionRecordSet.Entities.Clear();
                    EntityNameMap.Clear();
                    emrBatchSize = 200;

                    //_selectedWorkflow = null;
                    //_selectedView = null;

                    this.Invoke((MethodInvoker)delegate ()
                    {
                        cmbEntities.Items.Clear();                        
                        lstViews.Items.Clear();
                        radFetchXML.Checked = false;
                        radViews.Checked = true;
                        tsbCount.Enabled = tsbExecute.Enabled = false;
                        txtBatchSize.Text = "200";
                        txtInterval.Text = "0";
                        txtRecordCount.Clear();
                        rtxtFetchXML.Clear();
                        rtxtFetchXML.Enabled = false;
                        toolStripSeparator5.Visible = tsbtnFetchBuilder.Visible = false;
                    });  

                    #endregion Reset Variables

                    #region Get Entity Metadata

                    RetrieveAllEntitiesRequest _getAllEntities = new RetrieveAllEntitiesRequest()
                    {
                        EntityFilters = Microsoft.Xrm.Sdk.Metadata.EntityFilters.Entity,
                        RetrieveAsIfPublished = true
                    };

                    RetrieveAllEntitiesResponse response = (RetrieveAllEntitiesResponse)Service.Execute(_getAllEntities);

                    e.Result = response;

                    #endregion Get Entity Metadata
                },
                ProgressChanged = e =>
                {
                    // If progress has to be notified to user, use the following method:
                    SetWorkingMessage("Metadata retrieved!");
                },
                PostWorkCallBack = e =>
                {
                    allEntityMetadata.AddRange(((RetrieveAllEntitiesResponse)e.Result).EntityMetadata);

                    foreach (var item in allEntityMetadata)
                    {
                        if (item.IsValidForAdvancedFind == true)//item.DisplayName.LocalizedLabels.Count() > 0 &&  // && item.IsCustomizable.Value == true)
                        {
                            cmbEntities.Items.Add(string.Format("{0} ({1})", item.DisplayName.UserLocalizedLabel.Label.ToString(), item.LogicalName.ToLower()));
                            EntityNameMap.Add(string.Format("{0} ({1})", item.DisplayName.UserLocalizedLabel.Label.ToString(), item.LogicalName.ToLower()), item.LogicalName.ToLower());
                        }
                        //else
                        //{
                        //    cmbEntities.Items.Add(item.SchemaName);
                        //}
                    }

                    cmbEntities.Sorted = true;
                    cmbEntities.Text = "Select an Entity";
                },
                AsyncArgument = null,
                IsCancelable = true,
                MessageWidth = 340,
                MessageHeight = 150
            });
        }

        private void loadViews()
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = string.Format("Retrieving Views for {0}...", cmbEntities.SelectedItem.ToString()),
                Work = (w, e) =>
                {
                    #region Reset Variables

                    ExecutionRecordSet.Entities.Clear();
                    views.Entities.Clear();

                    this.Invoke((MethodInvoker)delegate ()
                    {
                        lstViews.Items.Clear();
                        radFetchXML.Checked = false;
                        radViews.Checked = true;
                        tsbCount.Enabled = false;
                        tsbExecute.Enabled = false;
                        txtRecordCount.Clear();
                        rtxtFetchXML.Clear();
                    });

                    #endregion Reset Variables

                    #region System Views Loop
                    string selectedEntity = "";
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        selectedEntity = cmbEntities.SelectedItem.ToString();
                    });

                    QueryExpression query = new QueryExpression("savedquery");
                    query.ColumnSet.AllColumns = true;
                    query.AddOrder("name", OrderType.Ascending);
                    query.Criteria = new FilterExpression();
                    //query.Criteria.AddCondition("returnedtypecode", ConditionOperator.Equal, workflowEntity);

                    FilterExpression childFilter = query.Criteria.AddFilter(LogicalOperator.And);
                    childFilter.AddCondition("querytype", ConditionOperator.Equal, 0);
                    childFilter.AddCondition("returnedtypecode", ConditionOperator.Equal, EntityNameMap[selectedEntity]);
                    childFilter.AddCondition("statecode", ConditionOperator.Equal, 0);
                    childFilter.AddCondition("fetchxml", ConditionOperator.NotNull);

                    EntityCollection _ManagedViews = Service.RetrieveMultiple(query);
                    views.Entities.AddRange(_ManagedViews.Entities);

                    foreach (var item in _ManagedViews.Entities)
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            lstViews.Items.Add(item["name"]);
                        });
                    }
                    #endregion System Views Loop

                    #region Personal View Divider

                    this.Invoke((MethodInvoker)delegate ()
                    {
                        lstViews.Items.Add("----------------------- Personal Views ----------------------------");
                    });

                    Entity _dummyView = new Entity("userquery");
                    _dummyView.Id = Guid.NewGuid();
                    _dummyView["name"] = "Dummy View";

                    views.Entities.Add(_dummyView);

                    #endregion Personal View Divider

                    #region Personal Views Loop
                    QueryExpression query2 = new QueryExpression("userquery");
                    query2.ColumnSet.AllColumns = true;
                    query.AddOrder("name", OrderType.Ascending);
                    query2.Criteria = new FilterExpression();
                    //query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, true);

                    FilterExpression childFilter2 = query2.Criteria.AddFilter(LogicalOperator.And);
                    childFilter2.AddCondition("querytype", ConditionOperator.Equal, 0);
                    childFilter2.AddCondition("returnedtypecode", ConditionOperator.Equal, EntityNameMap[selectedEntity]);
                    childFilter2.AddCondition("statecode", ConditionOperator.Equal, 0);
                    childFilter2.AddCondition("fetchxml", ConditionOperator.NotNull);

                    EntityCollection _UserViews = Service.RetrieveMultiple(query2);
                    views.Entities.AddRange(_UserViews.Entities);

                    foreach (var item in _UserViews.Entities)
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            lstViews.Items.Add(item["name"]);
                        });
                    }
                    #endregion Personal Views Loop

                    e.Result = views;

                },
                ProgressChanged = e =>
                {
                    // If progress has to be notified to user, use the following method:
                    SetWorkingMessage("Views retrieved!");
                },
                PostWorkCallBack = e =>
                {

                },
                AsyncArgument = null,
                IsCancelable = true,
                MessageWidth = 340,
                MessageHeight = 150
            });
        }

        private void getRecordCount()
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Counting Records in View...",
                Work = (w, e) =>
                {
                    ExecutionRecordSet.Entities.Clear();
                    //boolStopProcessing = false;

                    string fetchXml = "";
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        fetchXml = rtxtFetchXML.Text;
                        tsbtnStop.Enabled = true;
                    });

                    if (fetchXml.Contains("aggregate="))
                    {
                        e.Cancel = true;
                        MessageBox.Show("Cannot use Aggregate queries with this tool.", "Bulk Delete Tool - FetchXML Query Error");
                        //boolStopProcessing = false;
                        //ExecutionRecordSet.Entities.Clear();
                        //UIStatusUpdated("Query Ready");
                    }

                    var conversionRequest = new FetchXmlToQueryExpressionRequest
                    {
                        FetchXml = fetchXml
                    };

                    FetchXmlToQueryExpressionResponse conversionResponse;
                    try
                    {
                        conversionResponse = (FetchXmlToQueryExpressionResponse)Service.Execute(conversionRequest);
                    }
                    catch (Exception ex)
                    {
                        e.Cancel = true;
                        MessageBox.Show(ex.Message.ToString(), "Bulk Delete Tool - FetchXML Query Error");
                        //boolStopProcessing = false;
                        //ExecutionRecordSet.Entities.Clear();
                        //UIStatusUpdated("Query Ready");
                        return;
                        //throw;
                    }

                    QueryExpression query1 = conversionResponse.Query;
                    query1.ColumnSet.Columns.Clear();
                    query1.PageInfo = null;
                    if (query1.TopCount == null)
                    {
                        query1.PageInfo = new PagingInfo();
                        query1.PageInfo.PageNumber = 1;
                        query1.PageInfo.PagingCookie = null;
                        query1.PageInfo.Count = 5000;
                    }
                    else if (query1.TopCount > 5000)
                    {
                        e.Cancel = true;
                        MessageBox.Show("Cannot use 'Top' with more than 5000 records.", "Bulk Delete Tool - FetchXML Query Error");
                        //boolStopProcessing = false;
                        //ExecutionRecordSet.Entities.Clear();
                        //UIStatusUpdated("Query Ready");
                    }

                    while (true)
                    {
                        //if (boolStopProcessing)
                        //{
                        //    e.Cancel = true;
                        //    //MessageBox.Show("Processing Stopped", "Bulk Workflow Execution");
                        //    //tsbCancel.Enabled = false;
                        //    boolStopProcessing = false;
                        //    //toolStripSplitButton1.Enabled = true;
                        //    ExecutionRecordSet.Entities.Clear();
                        //    //UIStatusUpdated("Query Ready");
                        //    break;
                        //}
                        EntityCollection results = null;
                        try
                        {
                            results = Service.RetrieveMultiple(query1);
                        }
                        catch (Exception ex)
                        {
                            e.Cancel = true;
                            MessageBox.Show(ex.Message.ToString(), "Bulk Delete Tool - FetchXML Validate Query Error");
                            //UIStatusUpdated("Query Ready");
                            throw;
                        }

                        ExecutionRecordSet.Entities.AddRange(results.Entities);

                        if (results.MoreRecords)
                        {
                            query1.PageInfo.PageNumber++;
                            query1.PageInfo.PagingCookie = results.PagingCookie;
                        }
                        else
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                txtRecordCount.Text = "0";
                            });
                            break;
                        }

                        w.ReportProgress(0, string.Format("Counting Records in View...({0})", ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture)));
                    }

                    e.Result = ExecutionRecordSet.Entities.Count == 0 ? "0" : ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture);
                    //this.Invoke((MethodInvoker)delegate ()
                    //{
                    //    txtRecordCount.Text = ExecutionRecordSet.Entities.Count == 0 ? "0" : ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture);
                    //    if (ExecutionRecordSet.Entities.Count == 0)
                    //    {
                    //        //UIStatusUpdated("Query Ready");
                    //    }
                    //    else
                    //    {
                    //        //UIStatusUpdated("Ready");
                    //    }
                    //});
                },
                ProgressChanged = e =>
                {
                    // If progress has to be notified to user, use the following method:
                    SetWorkingMessage(e.UserState.ToString());
                },
                PostWorkCallBack = e =>
                {                    
                    tsbtnStop.Enabled = false;
                    if (!e.Cancelled && ExecutionRecordSet.Entities.Count > 0)
                    {
                        txtRecordCount.Text = e.Result.ToString();
                        tsbExecute.Enabled = true;                        
                    }
                },
                AsyncArgument = null,
                IsCancelable = true,
                MessageWidth = 340,
                MessageHeight = 150
            });
        }

        public void ProcessDeletes()
        {
            DateTime startTime = DateTime.Now;
            if (ExecutionRecordSet.Entities.Count == 0)
            {
                MessageBox.Show("No records found in this view to delete.", "Bulk Delete Tool");
                return;
            }
            WorkAsync(new WorkAsyncInfo
            {
                //Message = $"Deleting {ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture)} Records{Environment.NewLine}from{Environment.NewLine}{ExecutionRecordSet.Entities[0].LogicalName}",
                Message = GetDeleteStatusMessage(ExecutionRecordSet.Entities[0].LogicalName, 0, ExecutionRecordSet.Entities.Count, new TimeSpan()),
                Work = (w, e) =>
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        toolStripDropDownButton1.Enabled = tsbCount.Enabled = tsbExecute.Enabled = tsbtnHelp.Enabled = tsbtnFetchBuilder.Enabled = false;
                        tsbtnStop.Enabled = true;
                    });

                    #region Bulk Data API Stuff
                    // Create an ExecuteMultipleRequest object.
                    requestWithResults = new ExecuteMultipleRequest()
                    {
                        // Assign settings that define execution behavior: continue on error, return responses. 
                        Settings = new ExecuteMultipleSettings()
                        {
                            ContinueOnError = true,
                            ReturnResponses = false
                        },
                        // Create an empty organization request collection.
                        Requests = new OrganizationRequestCollection()
                    };
                    #endregion Bulk Data API Stuff
                    emrBatchSize = txtBatchSize.Text != "" ? Convert.ToInt32(txtBatchSize.Text) : 200;
                    emrCount = 0;

                    foreach (Entity d in ExecutionRecordSet.Entities)
                    {
                        // Stop if should stop
                        if (w.CancellationPending) {
                            return;
                        }
                        
                        DeleteRequest dr = new DeleteRequest
                        {
                            Target = d.ToEntityReference()
                        };

                        // Add in EMR stuff here
                        RunEMR(dr, w);

                        if (txtInterval.Text != "0" && !string.IsNullOrWhiteSpace(txtInterval.Text))
                        {
                            System.Threading.Thread.Sleep(Convert.ToInt16(txtInterval.Text) * 1000);  
                            // TODO: Implement Timer Later
                            /*
                            var statusChecker = new StatusChecker(Convert.ToInt32(txtInterval.Text));

                            // Create a timer that invokes CheckStatus after one second, and every 1 second thereafter.
                            Console.WriteLine("{0:h:mm:ss.fff} Creating timer.\n",
                                              DateTime.Now);
                            var stateTimer = new System.Threading.Timer(statusChecker.CheckStatus, w, 1000, 1000);

                            // When autoEvent signals, change the period to every half second.
                            //autoEvent.WaitOne();
                            //stateTimer.Change(0, 500);
                            //Console.WriteLine("\nChanging period to .5 seconds.\n");

                            // When autoEvent signals the second time, dispose of the timer.
                            //autoEvent.WaitOne();
                            stateTimer.Dispose();
                            //Console.WriteLine("\nDestroying timer.");
                            */
                        }
                    }
                    FlushEMR(w);

                    e.Result = ""; // response.UserId;
                },
                ProgressChanged = e =>
                {
                    // If progress has to be notified to user, use the following method:
                    SetWorkingMessage(e.UserState.ToString());
                },
                PostWorkCallBack = e =>
                {
                    //MessageBox.Show(string.Format("You are {0}", (Guid)e.Result));
                    tsbtnStop.Enabled = false;
                    toolStripDropDownButton1.Enabled = tsbCount.Enabled = tsbExecute.Enabled = tsbtnHelp.Enabled = tsbtnFetchBuilder.Enabled = true;

                    TimeSpan tsRunTime = DateTime.Now - startTime;

                    MessageBox.Show("Started @ " + startTime.ToShortTimeString() + Environment.NewLine
                    + "Finished @ " + DateTime.Now.ToShortTimeString() + Environment.NewLine 
                    + "Total Run Time: " + tsRunTime.ToString("hh\\:mm\\:ss") + Environment.NewLine + Environment.NewLine
                    + string.Format("Records Deleted: {0} / {1}", emrCount.ToString("#,#", CultureInfo.InvariantCulture), ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture))
                    + Environment.NewLine + "Errors: " + errorCount.ToString("#,#", CultureInfo.InvariantCulture)
                    , "Bulk Delete Tool");
                },
                AsyncArgument = null,
                IsCancelable = true,
                MessageWidth = 340,
                MessageHeight = 150
            });
        }

        private string GetDeleteStatusMessage(string entityName, int recordCount, int totalRecords, TimeSpan ts)
        {
            return $"Deleting from {entityName}{Environment.NewLine}" +
                              $"{recordCount.ToString("#,#", CultureInfo.InvariantCulture)} / {totalRecords.ToString("#,#", CultureInfo.InvariantCulture)}{Environment.NewLine}" +
                              $"Est. Time Remaining {Environment.NewLine}" +
                              $"{ts.ToString("hh\\:mm\\:ss")}";
        }

        #endregion Main Tool Methods

        #region Custom Helper Methods

        private void RunEMR(OrganizationRequest or, BackgroundWorker w)
        {
            //string message = "";
            requestWithResults.Requests.Add(or);
            emrCount++;
            if (requestWithResults.Requests.Count >= emrBatchSize)
            {
                DateTime start = DateTime.Now;

                //w.ReportProgress(0, string.Format("Starting Workflows: {0} of {1}{2}Est. Time Remaining: {3}", emrCount.ToString("#,#", CultureInfo.InvariantCulture)
                //, ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture), Environment.NewLine, estTS.ToString("hh\\:mm\\:ss")));
                //message = string.Format("Deleting Records: {0} of {1}{2}Est. Time Remaining: {3}", emrCount.ToString("#,#", CultureInfo.InvariantCulture)
                //    , ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture), Environment.NewLine, estTS.ToString("hh\\:mm\\:ss"));
                
                ExecuteMultipleResponse emrsp = (ExecuteMultipleResponse)Service.Execute(requestWithResults);
                HandleErrors(emrsp);
                requestWithResults.Requests.Clear();

                DateTime end = DateTime.Now;
                TimeSpan ts = end - start;
                _listTS.Add(ts);

                double doubleAverageTicks = _listTS.Average(timeSpan => timeSpan.Ticks);
                long longAverageTicks = Convert.ToInt64(doubleAverageTicks);
                avgTS = new TimeSpan(longAverageTicks);
                estTS = new TimeSpan(((ExecutionRecordSet.Entities.Count - emrCount) / emrBatchSize) * avgTS.Ticks);

                w.ReportProgress(0, GetDeleteStatusMessage(ExecutionRecordSet.Entities[0].LogicalName, emrCount, ExecutionRecordSet.Entities.Count, estTS));
                double progValue = (((double)emrCount / (double)ExecutionRecordSet.Entities.Count) * 100);
                //SendMessageToStatusBar?.Invoke(this, new StatusBarMessageEventArgs(Convert.ToInt32(Math.Round(progValue)), $"{emrCount.ToString("#,#", CultureInfo.InvariantCulture)} / {ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture)}"));
                SendMessageToStatusBar?.Invoke(this, new StatusBarMessageEventArgs(Convert.ToInt32(Math.Round(progValue)), null));                                
            }
            //return message;
        }

        private void FlushEMR(BackgroundWorker w)
        {
            if (emrCount > 0)
            {
                //w.ReportProgress(0, string.Format("Deleting Records: {0} / {1}{2}Est. Time Remaining: {3}", emrCount.ToString("#,#", CultureInfo.InvariantCulture)
                //, ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture), Environment.NewLine, estTS.ToString("hh\\:mm\\:ss")));
                w.ReportProgress(0, GetDeleteStatusMessage(ExecutionRecordSet.Entities[0].LogicalName, emrCount, ExecutionRecordSet.Entities.Count, estTS));
                double progValue = (((double)emrCount / (double)ExecutionRecordSet.Entities.Count) * 100);
                //SetWorkingMessage(string.Format("Starting Workflows: {0} of {1}{2}Est. Time Remaining: {3}", emrCount.ToString("#,#", CultureInfo.InvariantCulture)
                //    , ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture), Environment.NewLine, estTS.ToString("hh\\:mm\\:ss")));

                ExecuteMultipleResponse emrsp = (ExecuteMultipleResponse)Service.Execute(requestWithResults);
                HandleErrors(emrsp);
                requestWithResults.Requests.Clear();
                
                //tsbCancel.Enabled = false;

                //MessageBox.Show("Finished @ " + DateTime.Now.ToShortTimeString() + Environment.NewLine + Environment.NewLine
                //    + string.Format("Records Deleted: {0} of {1}", emrCount.ToString("#,#", CultureInfo.InvariantCulture), ExecutionRecordSet.Entities.Count.ToString("#,#", CultureInfo.InvariantCulture))
                //    + Environment.NewLine + "Errors: " + errorCount
                //    , "Bulk Delete Tool");
                return;
            }
            else
            {
                //MessageBox.Show("No records found in this view to process", "Bulk Workflow Execution");
                return;
            }
        }

        private void HandleErrors(ExecuteMultipleResponse emrsp)
        {
            bool beep = true;
            foreach (var response in emrsp.Responses)
            {
                if (response.Fault != null)
                {
                    if (beep)
                    {
                        //Console.Beep();
                        beep = false;
                    }
                    errorCount++;
                }
            }
        }

        #endregion Custom Helper Methods
        
        #region XrmToolBox Methods

        #region Who Am I Sample

        public void ProcessWhoAmI()
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Retrieving your user id...",
                Work = (w, e) =>
                {
                    var request = new WhoAmIRequest();
                    var response = (WhoAmIResponse)Service.Execute(request);

                    e.Result = response.UserId;
                },
                ProgressChanged = e =>
                {
                    // If progress has to be notified to user, use the following method:
                    SetWorkingMessage("Message to display");
                },
                PostWorkCallBack = e =>
                {
                    MessageBox.Show(string.Format("You are {0}", (Guid)e.Result));
                },
                AsyncArgument = null,
                IsCancelable = true,
                MessageWidth = 340,
                MessageHeight = 150
            });
        }

        private void BtnWhoAmIClick(object sender, EventArgs e)
        {
            ExecuteMethod(ProcessWhoAmI);
        }

        #endregion Who Am I Sample

        #region Github implementation

        public string RepositoryName
        {
            get { return "XrmToolBox---Bulk-Workflow-Execution"; }
        }

        public string UserName
        {
            get { return "andypopkin"; }
        }

        #endregion Github implementation
        
        #region PayPal implementation

        public string DonationDescription
        {
            get { return "Donate to inspire Andy to build more XrmToolBox tools!"; }
        }

        public string EmailAccount
        {
            get { return "fromasta@gmail.com"; }
        }

        #endregion PayPal implementation

        #region Help implementation

        public string HelpUrl
        {
            get { return "http://www.google.com"; }
        }

        #region MessageBus implementation

        public void OnIncomingMessage(MessageBusEventArgs message)
        {
            if (message.SourcePlugin == "FetchXML Builder" &&
                message.TargetArgument is string)
            {
                XDocument XDocument = XDocument.Parse((string)message.TargetArgument);
                rtxtFetchXML.Text = XDocument.ToString();
            }
        }


        #endregion MessageBus implementation

        #endregion Help implementation

        #endregion XrmToolBox Methods           

        class StatusChecker
        {
            private int invokeCount;
            private int maxCount;

            public StatusChecker(int count)
            {
                invokeCount = 0;
                maxCount = count;
            }

            // This method is called by the timer delegate.
            public void CheckStatus(Object stateInfo)
            {
                //AutoResetEvent autoEvent = (AutoResetEvent)stateInfo;
                Console.WriteLine("{0} Checking status {1}{2}.", DateTime.Now.ToString("h:mm:ss.fff"), (++invokeCount).ToString(), stateInfo.ToString());
                // TODO: make this work
                ((BackgroundWorker)stateInfo).ReportProgress(0, (++invokeCount).ToString()); // this would give us just the seconds - need all the values...
            }
        }
    }
}

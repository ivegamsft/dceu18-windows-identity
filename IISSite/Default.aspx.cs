﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Configuration;
using System.Security.Claims;
using System.Globalization;
using System.Data.SqlClient;
using System.Data;
using IISSite.Helpers;
using IISSite.Models;
using System.DirectoryServices;
using System.Web.Helpers;

namespace IISSite
{
    public partial class Default : System.Web.UI.Page
    {
       // private string fileShareRootPath;

        //string fileShareRootPath = string.Empty;
        //string fileShareVirtualDirectory = string.Empty;
        //string currentPath = @"\";
        //string decodedVal = string.Empty;
        //string fileShareConfigId = string.Empty;
        //DirectoryInfo currentDirectory = null;
        //DirectoryInfo[] curFolders = null;
        //FileInfo[] curDirFiles = null;
        //DirectoryInfo currentParent = null;
        //string FileSharePath = string.Empty;

        protected override void OnLoad(EventArgs e)
        {
            if (!Page.IsPostBack)
            {
                //ShowData.Attributes["class"] = "btn btn-link collapsed";
                toggleError();
                bindUserData();
                bindClaimsTab();
                bindGroupsTab();
                bindSqlTab();
                bindLdapTab();
                bindMsmqTab();
                bindFileshareTab();
            }
        }

        private void bindSqlTab()
        {
            //If there is a SQL connection string configured, set it in the UI
            string connString = ConfigurationManager.ConnectionStrings["RemoteSqlServer"]?.ToString();
            if (!string.IsNullOrEmpty(connString))
            {
                try
                {
                    SqlConnectionStringBuilder sqlConnectionString = new SqlConnectionStringBuilder(connString);
                    dbServerName.Text = sqlConnectionString.DataSource;
                    dbDatabaseName.Text = sqlConnectionString.InitialCatalog;
                    dbConnString.Text = sqlConnectionString.ToString();
                }
                catch
                {
                }
            }
        }

        private void bindLdapTab()
        {
            
        }

        private void bindMsmqTab()
        {
            
        }

        private void bindFileshareTab()
        {

        }

        private void bindUserData()
        {
            loginName.Text = Request.LogonUserIdentity.Name;
        }

        private void toggleError(string p)
        {
            errLabel.Text = p;
            errLabel.Visible = true;
        }

        private void toggleError()
        {
            errLabel.Text = string.Empty;
            errLabel.Visible = false;
        }
        private void bindClaimsTab()
        {
            ClaimsIdentity currentClaims = Request.LogonUserIdentity;

            string networkClaimType = "http://schemas.microsoft.com/ws/2012/01/insidecorporatenetwork";
            string userLocationLabel = "{0} the corporate network";
            string userLocation = "UNKNOWN";
            Claim sourceClaim = currentClaims.Claims.FirstOrDefault(c => c.Type.ToString(CultureInfo.InvariantCulture) == networkClaimType);
            if (sourceClaim != null)
            {
                if (sourceClaim.Value.ToUpper() == "TRUE")
                {
                    userLocation = "INSIDE";
                }
                else
                {
                    userLocation = "OUTSIDE";
                }
            }
            else
            {
                //Could not determine the network type
                userLocationLabel = "{0} network";
            }
            UserSourceLocation.Text = string.Format(userLocationLabel, userLocation);
            claimsGrid.DataSource = currentClaims.Claims;
            claimsGrid.AutoGenerateColumns = false;
            claimsGrid.DataBind();
        }

        private void bindGroupsTab()
        {
            List<ADGroup> groups = new List<ADGroup>();
            try
            {
                groups = LdapHelper.GetADGroups(Request.LogonUserIdentity.Groups);
                userGroupsGrid.DataSource = groups;
                userGroupsGrid.DataBind();
            }
            catch (Exception gex)
            {
                toggleError("An error occurred translating group names::" + gex.ToString());
            }
        }
        protected void GetData_Click(object sender, EventArgs e)
        {
            // Open the connection.
            sqlResults.Visible = true;
            //string sqlQuery = "SELECT TOP 1000 [Id],[ParentCategoryId],[Path],[Name],[NumActiveAds] FROM [Classifieds].[dbo].[Categories]";
            string sqlQuery = dbQuery.Text;

            try
            {
                SqlConnectionStringBuilder sqlConnectionString = new SqlConnectionStringBuilder();
                sqlConnectionString.DataSource = dbServerName.Text;
                sqlConnectionString.InitialCatalog = dbDatabaseName.Text;

                SqlConnection dbConnection = new SqlConnection(sqlConnectionString.ToString());
                // Run the SQL statement, and then get the returned rows to the DataReader.
                SqlDataAdapter dataadapter = new SqlDataAdapter(sqlQuery, dbConnection);
                DataSet ds = new DataSet();
                dbConnection.Open();
                dataadapter.Fill(ds, "data");
                dbConnection.Close();
                sqlDataGrid.DataSource = ds.Tables["data"].DefaultView;
                sqlDataGrid.DataBind();
            }
            catch (Exception sqlex)
            {
                toggleError(sqlex.ToString());
            }
        }

        protected void fileShareBind_Click(object sender, EventArgs e)
        {
            ClaimsIdentity currentClaims = Request.LogonUserIdentity;
            Claim upnClaim = currentClaims.Claims.FirstOrDefault(c => c.Type.ToString(CultureInfo.InvariantCulture) == ClaimTypes.Upn);
            //if (upnClaim != null)
            //{ 
            string serverName = fileShareServerName.Text;
            string fileFolderName = fileShareName.Text;
            string fileShareRootPath = $@"\\{serverName}\{fileFolderName}";
            FileShareItem rootFileShareItem = new FileShareItem()
            {
                ParentFileShare = serverName,
                ActualPath = fileShareRootPath,
                DisplayName = fileFolderName
                
            };
            BindFiles(rootFileShareItem);
            //}
            //else
            //{
            //    toggleError("No UPN");
            //}
        }
        private void BindFiles(FileShareItem fileShareItem)
        {
            //string serverName = fileShareServerName.Text;
            //string fileFolderName = fileShareName.Text;
            string fileShareRootPath = fileShareItem.ActualPath;

            //string fileShareVirtualDirectory = string.Empty;
            //string currentPath = @"\";
            //string decodedVal = string.Empty;
            //string fileShareConfigId = string.Empty;
            DirectoryInfo currentDirectory = null;
            DirectoryInfo[] curFolders = null;
            FileInfo[] curDirFiles = null;
            DirectoryInfo currentParent = null;

            //if (userUpn != null)
            //{
            try
            {
                //Impersonate the current user to get the files to browse
                //WindowsIdentity wi = new WindowsIdentity(userUpn.Value);
                //using (WindowsImpersonationContext wCtx = wi.Impersonate())
                //{
                currentDirectory = new DirectoryInfo(fileShareItem.ActualPath);
                if (System.IO.Directory.Exists(fileShareRootPath))
                {
                    curFolders = currentDirectory.GetDirectories();
                    curDirFiles = currentDirectory.GetFiles();
                    //if there are no children and we are not at the root, show parents
                    if (currentDirectory.Parent != null)
                    {
                        if (currentDirectory.Parent.FullName == fileShareRootPath)
                        {
                            //We are at the root
                            currentParent = currentDirectory;
                        }
                    }
                    else
                    {
                        //We are at the root
                        currentParent = currentDirectory;
                    }

                    directoryList.DataSource = curFolders;
                    directoryList.DataBind();

                    fileList.DataSource = curDirFiles;
                    fileList.DataBind();

                    //Build the breadcrumb for the page
                    BuildBreadcrumb(fileShareRootPath, fileShareRootPath);
                    //wCtx.Undo();
                }
                else
                {
                    //The configured directory does not exist or is invalid
                    //}
                }
            }
            catch (System.Security.SecurityException)
            {
                toggleError(string.Format("Access denied"));
            }
            catch (System.UnauthorizedAccessException)
            {
                //Do something with this error
                toggleError(string.Format("Access denied"));
            }
            //}
        }

        public void BuildBreadcrumb(string relativeFileSharePath, string RootFileShare)
        {
            //Remove the current root from the path and trim the extra backslashes
            string trimmedPath = relativeFileSharePath.Replace(RootFileShare, "").TrimStart('\\').TrimEnd('\\');
            string[] crumbPath = trimmedPath.Split('\\');
            string breadCrumbSepChar = " / ";
            ControlCollection links = new ControlCollection(breadcrumb);
            HyperLink link = new HyperLink
            {
                Text = RootFileShare,
                NavigateUrl = UrlUtility.BuildShareUri(RootFileShare).ToString()
            };
            links.Add(link);
            Label sepChar;
            string curPathNode = string.Empty;
            StringBuilder curPath = new StringBuilder();
            string currentNode = string.Empty;

            curPath.Append(@"\");
            for (int i = 0; i < crumbPath.Length; i++)
            {
                currentNode = crumbPath[i];

                if (i + 1 != crumbPath.Length)
                {
                    sepChar = new Label();
                    sepChar.Text = breadCrumbSepChar;
                    links.Add(sepChar);
                    link = new HyperLink
                    {
                        Text = currentNode
                    };
                    curPath.Append(currentNode);
                    curPath.Append(@"\");
                    link.NavigateUrl = UrlUtility.BuildFolderUri(curPath.ToString(), RootFileShare).ToString();
                    links.Add(link);
                }
                curPathNode = currentNode;
            }


            //If we are at the root, the trimmed path will be empty, dont add a >
            if (!string.IsNullOrEmpty(trimmedPath))
            {
                //Add the last node
                sepChar = new Label();
                sepChar.Text = breadCrumbSepChar;
                links.Add(sepChar);
                Label lastNode = new Label();
                lastNode.Text = curPathNode;
                links.Add(lastNode);
            }
        }

        protected void ldapBind_Click(object sender, EventArgs e)
        {
            string fqdnToSearch = ldapDomainname.Text;
            string searchString = Request.LogonUserIdentity.Name;

            try
            {
                DirectoryEntry rootEntry = new DirectoryEntry($@"LDAP://{fqdnToSearch}");
                rootEntry.AuthenticationType = System.DirectoryServices.AuthenticationTypes.Secure;
                SearchResultCollection searchResults = null;
                DirectorySearcher searcher = new DirectorySearcher(rootEntry);
                searcher.SearchScope = SearchScope.Subtree;
                searcher.SizeLimit = 50;

                switch (ldapUsersOrGroups.SelectedValue)
                {
                    case "users":
                        {
                            searcher.Filter = ("(&(objectCategory=person))");
                            break;
                        }
                    case "groups":
                        {
                            searcher.Filter = ("(&(objectCategory=group))");
                            break;
                        }
                }

                //TODO: If there are no results, toggle an error
                searchResults = searcher.FindAll();
                ldapDataGrid.DataSource = searchResults;
                ldapDataGrid.DataBind();

                //var queryFormat = "(&(objectClass=user)(objectCategory=person)(|(SAMAccountName=*{0}*)(cn=*{0}*)(gn=*{0}*)(sn=*{0}*)(email=*{0}*)))";
                //searcher.Filter = string.Format(queryFormat, searchString);
                //var searchResults = searcher.FindAll();
                ////TODO: If there are no results, toggle an error
                //ldapDataGrid.DataSource = searchResults;
                //ldapDataGrid.DataBind();
            }
            catch (Exception ex)
            {
                toggleError(ex.ToString());
            }
        }

        protected void fileList_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            RepeaterItem parentItem = e.Item;
            FileInfo currentFile;
            if (parentItem.ItemType == ListItemType.Item || parentItem.ItemType == ListItemType.AlternatingItem)
            {
                currentFile = parentItem.DataItem as FileInfo;
                HyperLink fileName = parentItem.FindControl("fileName") as HyperLink;
                Label size = parentItem.FindControl("fileSize") as Label;
                Label ext = parentItem.FindControl("fileExt") as Label;
                Label attributes = parentItem.FindControl("fileAttributes") as Label;
                Label created = parentItem.FindControl("fileCreated") as Label;
                Label lastModifed = parentItem.FindControl("fileLastModified") as Label;
                //HtmlButton shareLink = parentItem.FindControl("shareLink") as HtmlButton;
                string fileDisplayName;
                string filePath;
                string fileUrl;
                //Make the file name relative
                if (currentFile != null)
                {
                    fileDisplayName = currentFile.Name;
                    filePath = currentFile.DirectoryName;
                    //fileUrl = currentFile.FullName;
                    fileName.Text = fileDisplayName;
                    fileName.ToolTip = filePath;
                    fileUrl = Server.UrlPathEncode(UrlUtility.BuildFileUri(currentFile.FullName, currentFile.DirectoryName).ToString());
                    fileName.NavigateUrl = fileUrl;
                    size.Text = FileShareHelper.DisplayFileSizeFromBytes(currentFile.Length);
                    ext.Text = currentFile.Extension;
                    attributes.Text = currentFile.Attributes.ToString();
                    created.Text = currentFile.CreationTime.ToString();
                    lastModifed.Text = currentFile.LastWriteTime.ToString();
                    //shareLink.Attributes.Add("href", "sharelink.aspx?u=" + fileUrl);
                }
            }
        }

        protected void directoryList_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            RepeaterItem parentItem = e.Item;
            DirectoryInfo currentFolder;
            if (parentItem.ItemType == ListItemType.Item || parentItem.ItemType == ListItemType.AlternatingItem)
            {
                currentFolder = parentItem.DataItem as DirectoryInfo;
                LinkButton folderName = parentItem.FindControl("folderName") as LinkButton;
                Label count = parentItem.FindControl("fileCount") as Label;
                Label created = parentItem.FindControl("folderCreated") as Label;
                Label lastModifed = parentItem.FindControl("folderLastModified") as Label;

                //Make the file name relative
                if (currentFolder != null)
                {
                    //Make the URL relative
                    //see if we have access to the sub folders. if not, do something
                    try
                    {
                        count.Text = currentFolder.EnumerateFileSystemInfos().Count().ToString();
                        folderName.Text = string.Format("<span class='glyphicon {0}'></span>&nbsp;<span class=''>{1}</span>", "glyphicon-ok-circle", currentFolder.Name);
                        FileShareItem fs = new FileShareItem()
                        {
                            ActualPath = currentFolder.FullName,
                            DisplayName = currentFolder.Name,
                            ParentFileShare = currentFolder.Root.Name
                        };

                        if (currentFolder.GetDirectories().Length > 0)
                        {
                            //Folder has sub folders, enumerate those
                        }
                        folderName.CommandArgument = Json.Encode(fs);
                        folderName.CommandName = "listfiles";
                        created.Text = currentFolder.CreationTime.ToString();
                        lastModifed.Text = currentFolder.LastWriteTime.ToString();
                    }
                    catch
                    {
                        //User does not have access to the file share.
                        folderName.Text = string.Format("<span class='glyphicon {0}'></span>&nbsp;<span class=''>{1}</span>", "glyphicon-ban-circle", currentFolder.Name);
                    }
                }
            }
        }

        protected void fileShareList_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            HyperLink curLink = null;
            FileShareItem curFileShare = null;
            if (e.Item.ItemType.Equals(ListItemType.Item) || e.Item.ItemType.Equals(ListItemType.AlternatingItem))
            {
                curFileShare = (FileShareItem)e.Item.DataItem;
                curLink = e.Item.FindControl("fileShareUrl") as HyperLink;
                curLink.NavigateUrl = UrlUtility.BuildShareUri(curFileShare.DisplayName).ToString();
                curLink.Text = curFileShare.DisplayName;
            }
        }

        protected void directoryList_ItemCommand(object sender, RepeaterCommandEventArgs e)
        {
            if (e.CommandName == "listfiles")
            {
                // Get the value of command argument
                var value = e.CommandArgument;
                // Do whatever operation you want.  
                FileShareItem fs = Json.Decode<FileShareItem>(e.CommandArgument.ToString());
                BindFiles(fs);
            }
        }

        protected void createRandomFile_Click(object sender, EventArgs e)
        {
            //setFileShareConfigContext();
            //if (curFileShareConfig != null)
            //{
            //    //TODO: Validate this URL
            //    //fileShareRootPath = curFileShareConfig.ActualPath;
            //    fullFileSharePath = fileShareRootPath + currentPath;

            //    //Impersonate the current user to get the files to browse
            //    if (!String.IsNullOrEmpty(fullFileSharePath))
            //    {
            //        currentDirectory = new DirectoryInfo(fullFileSharePath);
            //        if (System.IO.Directory.Exists(fullFileSharePath))
            //        {
            //            ClaimsPrincipal currentClaims = ClaimsPrincipal.Current;
            //            Claim userUpn = currentClaims.Claims.FirstOrDefault(c => c.Type.ToString(CultureInfo.InvariantCulture) == ClaimTypes.Upn);

            //            if (userUpn != null)
            //            {
            //                WindowsIdentity wi = new WindowsIdentity(userUpn.Value);
            //                using (WindowsImpersonationContext wCtx = wi.Impersonate())
            //                {
            //                    try
            //                    {
            //                        System.IO.File.CreateText(currentDirectory.FullName + "\\" + Path.GetRandomFileName());
            //                        wCtx.Undo();
            //                    }
            //                    catch (System.UnauthorizedAccessException)
            //                    {
            //                        //Do something with this error
            //                    }
            //                }
            //                //Rebind the UI
            //                bindFiles(userUpn);
            //            }
            //        }
            //    }
        }
    }
}
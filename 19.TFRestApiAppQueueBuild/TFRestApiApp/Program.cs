﻿using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string TFUrl = "https://dev.azure.com/<organization>/";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            try
            {
                string TeamProjectName = "<Team Project Name>";
                int buildId = 28; // update to an existing build definition id
                string artifactName = "drop"; //default artifact name


                ConnectWithPAT(TFUrl, UserPAT);

                var startedBuild = QueueBuild(TeamProjectName, buildId); 
                Console.WriteLine("Build has been started: " + startedBuild.BuildNumber);
                if (WaitEndOfBuild(TeamProjectName, startedBuild.Id))
                {
                    PrintTimeLine(TeamProjectName, startedBuild.Id);
                    DownloadBuildResults(startedBuild, artifactName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Download a build content
        /// </summary>
        /// <param name="StartedBuild"></param>
        /// <param name="ArtifactName"></param>
        private static void DownloadBuildResults(Build StartedBuild, string ArtifactName)
        {
            BuildArtifact drop = BuildClient.GetArtifactAsync(StartedBuild.Id, ArtifactName).Result; //get detiled info

            Console.WriteLine("Build location: " + drop.Resource.DownloadUrl);

            string dropFileName = String.Format("{0}_{1}.zip", StartedBuild.Definition.Name, StartedBuild.BuildNumber);

            Stream zipStream = BuildClient.GetArtifactContentZipAsync(StartedBuild.Id, ArtifactName).Result; //get content

            using (FileStream zipFile = new FileStream(dropFileName, FileMode.Create))
                zipStream.CopyTo(zipFile);

            Console.WriteLine("The file '{0}' has been downloaded.", dropFileName);
        }

        /// <summary>
        /// Queue new build
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="BuildDefId"></param>
        /// <returns></returns>
        private static Build QueueBuild(string TeamProjectName, int BuildDefId)
        {
            var buildDefinition = BuildClient.GetDefinitionAsync(TeamProjectName, BuildDefId).Result;
            var teamProject = ProjectClient.GetProject(TeamProjectName).Result;
            return BuildClient.QueueBuildAsync(new Build() { Definition = buildDefinition, Project = teamProject }).Result;
        }

        /// <summary>
        /// Wait end of build
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="BuildId"></param>
        private static bool WaitEndOfBuild(string TeamProjectName, int BuildId)
        {            
            int countWait = 0;
            string lastStatus = "";

            Build buildRun;

            while (true)
            {
                buildRun = BuildClient.GetBuildAsync(TeamProjectName, BuildId).Result;

                if (buildRun.Status.Value.ToString() != lastStatus)
                {
                    lastStatus = buildRun.Status.Value.ToString();
                    Console.WriteLine("\nCurrent Status: " + lastStatus);
                }
                else
                    Console.Write(".");

                if (buildRun.Status.Value == BuildStatus.Completed) break;
                if (buildRun.Status.Value == BuildStatus.Cancelling) return false;

                if (countWait > 200) { Console.WriteLine("\nI cann`t wait!"); return false; }

                Thread.Sleep(2000);
                countWait++;
            }

            Console.WriteLine();
            Console.WriteLine(buildRun.Status);

            return true;
        }

        /// <summary>
        /// Print completed steps of build
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="BuildId"></param>
        static void PrintTimeLine(string TeamProjectName, int BuildId)
        {
            var timeline = BuildClient.GetBuildTimelineAsync(TeamProjectName, BuildId).Result;

            if (timeline.Records.Count > 0)
            {
                Console.WriteLine("Task Name-----------------------------Start Time---Finish Time---Result");
                foreach(var record in timeline.Records)
                    if (record.RecordType == "Task")
                    Console.WriteLine("{0, -35} | {1, -10} | {2, -10} | {3}",
                        (record.Name.Length < 35) ? record.Name : record.Name.Substring(0, 35), 
                        (record.StartTime.HasValue) ? record.StartTime.Value.ToLongTimeString() : "",
                        (record.FinishTime.HasValue) ? record.FinishTime.Value.ToLongTimeString() : "",
                        (record.Result.HasValue) ? record.Result.Value.ToString() : "");
            }
        }
       
        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            GitClient = Connection.GetClient<GitHttpClient>();
            TfvsClient = Connection.GetClient<TfvcHttpClient>();
            TestManagementClient = Connection.GetClient<TestManagementHttpClient>();
        }

        static void ConnectWithDefaultCreds(string ServiceURL)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new VssCredentials());
            InitClients(connection);
        }

        static void ConnectWithCustomCreds(string ServiceURL, string User, string Password)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new WindowsCredential(new NetworkCredential(User, Password)));
            InitClients(connection);
        }

        static void ConnectWithPAT(string ServiceURL, string PAT)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new VssBasicCredential(string.Empty, PAT));
            InitClients(connection);
        }
    }
}

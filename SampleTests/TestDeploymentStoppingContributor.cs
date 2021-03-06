﻿
//------------------------------------------------------------------------------
// <copyright company="Microsoft">
//   Copyright 2013 Microsoft
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Public.Dac.Samples;
using Public.Dac.Samples.Contributors;
using System;
using System.Data.SqlClient;
using System.IO;

namespace Public.Dac.Sample.Tests
{
    [TestClass]
    public class TestDeploymentStoppingContributor
    {
        private const string DataSourceName = "(localdb)\\MSSQLLocalDB";
        private static string ServerConnectionString
        {
            get { return "Data Source=" + DataSourceName + ";Integrated Security=True"; }
        }

        public TestContext TestContext { get; set; }
        
        private DisposableList _trash;
        private string _dacpacPath;

        [TestInitialize]
        public void InitializeTest()
        {
            Directory.CreateDirectory(GetTestDir());

            _trash = new DisposableList();
            _dacpacPath = GetTestFilePath("myDatabase.dacpac");
            using (TSqlModel model = new TSqlModel(SqlServerVersion.Sql110, null))
            {
                model.AddObjects("CREATE TABLE [dbo].[t1] (c1 INT NOT NULL PRIMARY KEY)");
                DacPackageExtensions.BuildPackage(_dacpacPath, model, new PackageMetadata());
            }
        }

        [TestCleanup]
        public void CleanupTest()
        {
            _trash.Dispose();
            DeleteIfExists(_dacpacPath);
        }

        private string GetTestFilePath(string fileName)
        {
            return Path.Combine(GetTestDir(), fileName);
        }

        private string GetTestDir()
        {
            return Path.Combine(Environment.CurrentDirectory, TestContext.TestName);
        }

        [TestMethod]
        public void TestStopDeployment()
        {

            // Given database name
            string dbName = TestContext.TestName;

            // Delete any existing artifacts from a previous run
            TestUtils.DropDatabase(ServerConnectionString, dbName);

            // When deploying using the deployment stopping contributor
            try
            {
                DacDeployOptions options = new DacDeployOptions
                {
                    AdditionalDeploymentContributors = DeploymentStoppingContributor.ContributorId
                };

                using (DacPackage dacpac = DacPackage.Load(_dacpacPath, DacSchemaModelStorageType.Memory))
                {
                    const string connectionString = "Data Source=" + DataSourceName + ";Integrated Security=True";
                    DacServices dacServices = new DacServices(connectionString);

                    // Script then deploy, to support debugging of the generated plan
                    try
                    {
                        dacServices.GenerateDeployScript(dacpac, dbName, options);
                        Assert.Fail("Expected Deployment to fail and exception to be thrown");
                    }
                    catch (DacServicesException expectedException)
                    {
                        Assert.IsTrue(expectedException.Message.Contains(DeploymentStoppingContributor.ErrorViaPublishMessage),
                            "Expected Severity.Error message passed to base.PublishMessage to block deployment");
                        Assert.IsTrue(expectedException.Message.Contains(DeploymentStoppingContributor.ErrorViaThrownException),
                            "Expected thrown exception to block deployment");
                    }
                }

                // Also expect the deployment to fail
                AssertDeployFailed(ServerConnectionString, dbName);
            }
            finally
            {
                TestUtils.DropDatabase(ServerConnectionString, dbName);
            }
        }
        
        private void AssertDeployFailed(string dbConnectionString, string dbName)
        {
            SqlConnectionStringBuilder scsb = new SqlConnectionStringBuilder(dbConnectionString);
            scsb.InitialCatalog = "master";
            scsb.Pooling = false;
            using (SqlConnection conn = new SqlConnection(scsb.ConnectionString))
            {
                conn.Open();
                Assert.IsFalse(TestUtils.DoesDatabaseExist(conn, dbName));
            }
        }

        private static void DeleteIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}

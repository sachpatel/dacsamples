﻿//------------------------------------------------------------------------------
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
using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace Public.Dac.Samples.App
{
    /// <summary>
    /// Runs a simple test that runs code analysis against:
    /// A set of scripts,
    /// 
    /// </summary>
    internal sealed class RunCodeAnalysisExample
    {
        private static readonly Tuple<string,string>[] SampleScripts = new Tuple<string, string>[]
            {
                Tuple.Create("CREATE TABLE T1 (c1 int)", "NoProblems.sql"),
                Tuple.Create(@"CREATE VIEW [dbo].[View1] AS SELECT * FROM [dbo].[T1]", "OneProblem.sql"),
                Tuple.Create(@"CREATE PROCEDURE [dbo].[Procedure1]
AS
	SELECT WillCauseWarningOnValidate from NonexistentTable
RETURN 0", "ProcedureWithValidationWarnings.sql"),
            };


        /// <summary>
        /// Runs the model filtering example. This shows how to filter a model and save a new
        /// dacpac with the updated model. You can also update the model in the existing dacpac;
        /// the unit tests in TestFiltering.cs show how this is performed.
        /// </summary>
        public static void RunAnalysisExample()
        {

            // Given a model with objects that use "dev", "test" and "prod" schemas
            string scaPackagePath = GetFilePathInCurrentDirectory("sca.dacpac");
            string scriptAnalysisPath = GetFilePathInCurrentDirectory("scriptResults.xml");
            string dacpacAnalysisPath = GetFilePathInCurrentDirectory("dacpacResults.xml");
            string dbAnalysisPath = GetFilePathInCurrentDirectory("databaseResults.xml");
            var scripts = SampleScripts;
            using (TSqlModel model = new TSqlModel(SqlServerVersion.Sql120, new TSqlModelOptions()))
            {
                AddScriptsToModel(model, scripts);

                // Analyze scripted model
                RunAnalysis(model, scriptAnalysisPath);

                // Save dacpac to 
                Console.WriteLine("Saving scripts to package '" + scaPackagePath + "'");
                DacPackageExtensions.BuildPackage(scaPackagePath, model, new PackageMetadata());
            }

            RunDacpacAnalysis(scaPackagePath, dacpacAnalysisPath);
            RunAnalysisAgainstDatabase(scaPackagePath, dbAnalysisPath);
        }

        /// <summary>
        /// Runs analysis, writing the output to a file
        /// </summary>
        private static void RunAnalysis(TSqlModel model, string resultsFilePath)
        {
            // Creating a default service will run all discovered rules, treating issues as Warnings.
            // To configure which rules are run you can pass a CodeAnalysisRuleSettings object to the service
            // or as part of the CodeAnalysisServiceSettings passed into the factory method. Examples of this
            // can be seen in the RuleTest.CreateCodeAnalysisService method.

            CodeAnalysisService service = new CodeAnalysisServiceFactory().CreateAnalysisService(model.Version);
            service.ResultsFile = resultsFilePath;
            CodeAnalysisResult result = service.Analyze(model);
            Console.WriteLine("Code Analysis with output file {0} complete, analysis succeeded? {1}", 
                resultsFilePath, result.AnalysisSucceeded);
            PrintProblemsAndValidationErrors(model, result);
        }

        /// <summary>
        /// When running analysis agaisnt a Dacpac, be sure to specific loadAsScriptBackedModel=true when loading the 
        /// model
        /// </summary>
        private static void RunDacpacAnalysis(string packagePath, string resultsFilePath)
        {
            using (TSqlModel model = TSqlModel.LoadFromDacpac(packagePath,
                new ModelLoadOptions(DacSchemaModelStorageType.Memory, loadAsScriptBackedModel: true)))
            {
                RunAnalysis(model, resultsFilePath);
            }
        }

        /// <summary>
        /// Currently there is supported method for creating a TSqlModel by targeting a database. However extracting
        /// the database to a dacpac is supported, and this is the best way to do analysis against that database.
        /// Note that 
        /// </summary>
        private static void RunAnalysisAgainstDatabase(string productionPackagePath, string resultsFilePath)
        {
            string extractedPackagePath = GetFilePathInCurrentDirectory("extracted.dacpac");
            using (DacPackage package = DacPackage.Load(productionPackagePath, DacSchemaModelStorageType.Memory))
            {
                Console.WriteLine("Deploying the production dacpac to 'ProductionDB'");
                DacServices services = new DacServices("Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;");
                services.Deploy(package, "ProductionDB", true);

                Console.WriteLine("Extracting the 'ProductionDB' back to a dacpac for comparison");
                services.Extract(extractedPackagePath, "ProductionDB", "AppName", new Version(1, 0));
            }

            RunDacpacAnalysis(extractedPackagePath, resultsFilePath);
        }

        private static void PrintProblemsAndValidationErrors(TSqlModel model, CodeAnalysisResult analysisResult)
        {
            Console.WriteLine("-----------------");
            Console.WriteLine("Outputting validation issues and problems");
            foreach (var issue in model.Validate())
            {
                Console.WriteLine( "\tValidation Issue: '{0}', Severity: {1}", 
                    issue.Message,
                    issue.MessageType);
            }

            foreach (var problem in analysisResult.Problems)
            {
                Console.WriteLine("\tCode Analysis Problem: '{0}', Severity: {1}, Source: {2}, StartLine/Column [{3},{4}]",
                    problem.ErrorMessageString,
                    problem.Severity,
                    problem.SourceName,
                    problem.StartLine,
                    problem.StartColumn);
            }
            Console.WriteLine("-----------------");
        }


        private static void AddScriptsToModel(TSqlModel model, IEnumerable<Tuple<string,string>> scriptsWithNamedSources)
        {
            foreach (var scriptWithName in scriptsWithNamedSources)
            {
                model.AddOrUpdateObjects(scriptWithName.Item1, scriptWithName.Item2, null);
            }
        }
        
        private static string GetFilePathInCurrentDirectory(string fileName)
        {
            return Path.Combine(Environment.CurrentDirectory, fileName);
        }

    }
}

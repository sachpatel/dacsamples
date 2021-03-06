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

using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Public.Dac.Samples.Rules
{
    /// <summary>
    /// This is a SQL rule which returns a warning message 
    /// whenever a WAITFOR DELAY statement appears inside a subroutine body. 
    /// This rule only applies to SQL stored procedures, functions and triggers.
    /// 
    /// Note that this uses a Localized export attribute, and hence the rule name and description will be
    /// localized if resource files for different languages are used
    /// </summary>
    [LocalizedExportCodeAnalysisRule(AvoidWaitForDelayRule.RuleId,
        RuleConstants.ResourceBaseName,                     // Name of the resource file to look up displayname and description in
        RuleConstants.AvoidWaitForDelay_RuleName,              // ID used to look up the display name inside the resources file
        RuleConstants.AvoidWaitForDelay_ProblemDescription,    // ID used to look up the description inside the resources file
        Category = RuleConstants.CategoryPerformance,            // Rule category (e.g. "Design", "Naming")
        RuleScope = SqlRuleScope.Element)]                  // This rule targets specific elements rather than the whole model
    public sealed class AvoidWaitForDelayRule : SqlCodeAnalysisRule
    {
        /// <summary>
        /// The Rule ID should resemble a fully-qualified class name. In the Visual Studio UI
        /// rules are grouped by "Namespace + Category", and each rule is shown using "Short ID: DisplayName".
        /// For this rule, that means the grouping will be "Public.Dac.Samples.Performance", with the rule
        /// shown as "SR1004: Avoid using WaitFor Delay statements in stored procedures, functions and triggers."
        /// </summary>
        public const string RuleId = "Public.Dac.Samples.SR1004";

        public AvoidWaitForDelayRule()
        {
            // This rule supports Procedures, Functions and Triggers. Only those objects will be passed to the Analyze method
            SupportedElementTypes = new[]
            {
                // Note: can use the ModelSchema definitions, or access the TypeClass for any of these types
                ModelSchema.ExtendedProcedure,
                ModelSchema.Procedure,
                ModelSchema.TableValuedFunction,
                ModelSchema.ScalarFunction,

                ModelSchema.DatabaseDdlTrigger,
                ModelSchema.DmlTrigger,
                ModelSchema.ServerDdlTrigger
            };
        }

        /// <summary>
        /// For element-scoped rules the Analyze method is executed once for every matching object in the model. 
        /// </summary>
        /// <param name="ruleExecutionContext">The context object contains the TSqlObject being analyzed, a TSqlFragment
        /// that's the AST representation of the object, the current rule's descriptor, and a reference to the model being
        /// analyzed.
        /// </param>
        /// <returns>A list of problems should be returned. These will be displayed in the Visual Studio error list</returns>
        public override IList<SqlRuleProblem> Analyze(SqlRuleExecutionContext ruleExecutionContext)
        {
            IList<SqlRuleProblem> problems = new List<SqlRuleProblem>();

            TSqlObject modelElement = ruleExecutionContext.ModelElement;
            
            // this rule does not apply to inline table-valued function
            // we simply do not return any problem in that case.
            if (IsInlineTableValuedFunction(modelElement))
            {
                return problems;
            }

            string elementName = RuleUtils.GetElementName(ruleExecutionContext, modelElement);
            
            // The rule execution context has all the objects we'll need, including the fragment representing the object,
            // and a descriptor that lets us access rule metadata
            TSqlFragment fragment = ruleExecutionContext.ScriptFragment;
            RuleDescriptor ruleDescriptor = ruleExecutionContext.RuleDescriptor;

            // To process the fragment and identify WAITFOR DELAY statements we will use a visitor
            // 
            WaitForDelayVisitor visitor = new WaitForDelayVisitor();
            fragment.Accept(visitor);
            IList<WaitForStatement> waitforDelayStatements = visitor.WaitForDelayStatements;

            // Create problems for each WAITFOR DELAY statement found 
            foreach (WaitForStatement waitForStatement in waitforDelayStatements)
            {
                // When creating a rule problem, always include the TSqlObject being analyzed. This is used to determine
                // the name of the source this problem was found in and a best guess as to the line/column the problem was found at
                //
                // In addition if you have a specific TSqlFragment that is related to the problem also include this
                // since the most accurate source position information (start line and column) will be read from the fragment
                SqlRuleProblem problem = new SqlRuleProblem(
                                            String.Format(CultureInfo.CurrentCulture, ruleDescriptor.DisplayDescription, elementName),
                                            modelElement,
                                            waitForStatement);
                problems.Add(problem);
            }
            return problems;
        }

        private static bool IsInlineTableValuedFunction(TSqlObject modelElement)
        {
            return TableValuedFunction.TypeClass.Equals(modelElement.ObjectType)
                   && FunctionType.InlineTableValuedFunction == modelElement.GetMetadata<FunctionType>(TableValuedFunction.FunctionType);
        }

    }
}

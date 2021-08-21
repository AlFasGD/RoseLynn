﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using RoseLynn.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Resources;

namespace RoseLynn.Analyzers
{
    /// <summary>Provides mechanisms to function a storage of <seealso cref="DiagnosticDescriptor"/> instances that are grouped by analyzer type.</summary>
    public abstract class DiagnosticDescriptorStorageBase
    {
        private readonly Dictionary<Type, HashSet<DiagnosticDescriptor>> analyzerGroupedDiagnostics = new();
        private readonly Dictionary<string, DiagnosticDescriptor> diagnosticsByID = new();

        private Dictionary<Type, HashSet<DiagnosticDescriptor>> AnalyzerGroupedDiagnostics
        {
            get
            {
                if (analyzerGroupedDiagnostics.Count is 0)
                    AnalyzeDiagnostics();

                return analyzerGroupedDiagnostics;
            }
        }
        private Dictionary<string, DiagnosticDescriptor> DiagnosticsByID
        {
            get
            {
                if (diagnosticsByID.Count is 0)
                    AnalyzeDiagnostics();

                return diagnosticsByID;
            }
        }

        private void AnalyzeDiagnostics()
        {
            int ruleIDLength = GetDiagnosticID(0).Length;

            var thisType = GetType();
            var fields = thisType.GetFields();
            var properties = thisType.GetProperties();
            var fieldsOrProperties = fields.AsEnumerable<MemberInfo>().Concat(properties);

            foreach (var fieldOrProperty in fieldsOrProperties)
            {
                // All rule fields must have the DiagnosticSupportedAttribute
                var type = fieldOrProperty.GetCustomAttribute<DiagnosticSupportedAttribute>()?.DiagnosticAnalyzerType;
                if (type is null)
                    continue;

                var ruleID = fieldOrProperty.Name.Substring(0, ruleIDLength);
                var diagnosticDescriptor = fieldOrProperty.GetFieldOrPropertyValue(this) as DiagnosticDescriptor;

                diagnosticsByID.Add(ruleID, diagnosticDescriptor);

                if (!analyzerGroupedDiagnostics.TryGetValue(type, out var set))
                    analyzerGroupedDiagnostics.Add(type, set = new());

                set.Add(diagnosticDescriptor);
            }
        }

        /// <summary>Gets the <seealso cref="DiagnosticDescriptor"/> representing the given rule ID.</summary>
        /// <param name="ruleID">The rule ID whose <seealso cref="DiagnosticDescriptor"/> to get. The numeric portion of the rule ID will be combined with <seealso cref="DiagnosticIDPrefix"/>.</param>
        /// <returns>The <seealso cref="DiagnosticDescriptor"/> representing the given rule ID, if it exists, otherwise <see langword="null"/>.</returns>
        public DiagnosticDescriptor GetDiagnosticDescriptor(int ruleID)
        {
            return GetDiagnosticDescriptor(GetDiagnosticID(ruleID));
        }
        /// <summary>Gets the <seealso cref="DiagnosticDescriptor"/> representing the given rule ID.</summary>
        /// <param name="ruleID">The rule ID whose <seealso cref="DiagnosticDescriptor"/> to get.</param>
        /// <returns>The <seealso cref="DiagnosticDescriptor"/> representing the given rule ID, if it exists, otherwise <see langword="null"/>.</returns>
        public DiagnosticDescriptor GetDiagnosticDescriptor(string ruleID)
        {
            DiagnosticsByID.TryGetValue(ruleID, out var value);
            return value;
        }

        public IDictionary<Type, ImmutableArray<DiagnosticDescriptor>> GetDiagnosticDescriptorsByAnalyzersImmutable()
        {
            return AnalyzerGroupedDiagnostics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToImmutableArray());
        }
        public ImmutableArray<DiagnosticDescriptor> GetDiagnosticDescriptors<T>()
            where T : DiagnosticAnalyzer
        {
            return GetDiagnosticDescriptors(typeof(T));
        }
        public ImmutableArray<DiagnosticDescriptor> GetDiagnosticDescriptors(Type diagnosticAnalyzerType)
        {
            AnalyzerGroupedDiagnostics.TryGetValue(diagnosticAnalyzerType, out var set);
            return set.ToImmutableArray();
        }

        #region Diagnotsic Descriptor Construction
        /// <summary>Gets the URI for the base rule documentation directory.</summary>
        /// <remarks>The final directory separator does not have to be included.</remarks>
        protected abstract string BaseRuleDocsURI { get; }
        /// <summary>Gets the prefix of the diagnostic IDs of this storage.</summary>
        /// <remarks>Multiple analyzers may emit diagnostics whose diagnostic ID prefix is the same. For different categories of diagnostics, a new storage type must be created.</remarks>
        protected abstract string DiagnosticIDPrefix { get; }

        /// <summary>Gets the <seealso cref="System.Resources.ResourceManager"/> that contains the string resources for the diagnostics that are stored.</summary>
        protected abstract ResourceManager ResourceManager { get; }

        /// <summary>Creates a <seealso cref="DiagnosticDescriptor"/> from a diagnostic ID, a category, and a severity.</summary>
        /// <param name="id">The numeric portion of the diagnostic ID. The numeric value will be expanded to 4 decimal digits.</param>
        /// <param name="category">The category of the diagnostic.</param>
        /// <param name="severity">The severity of the diagnostic.</param>
        /// <returns>The resulting created <seealso cref="DiagnosticDescriptor"/>.</returns>
        protected DiagnosticDescriptor CreateDiagnosticDescriptor(int id, string category, DiagnosticSeverity severity)
        {
            return new DiagnosticDescriptor(GetDiagnosticID(id), GetTitle(id), GetMessageFormat(id), category, severity, true, helpLinkUri: GetHelpLinkURI(id), description: GetDescription(id));
        }

        /// <summary>Gets the resource string associated with the specified diagnostic ID and property.</summary>
        /// <param name="id">The diagnostic ID.</param>
        /// <param name="property">The string property associated to the rule.</param>
        /// <returns>The resource string associated with the given values.</returns>
        protected virtual LocalizableString GetResourceString(int id, string property)
        {
            return ResourceManager.GetString($"{GetDiagnosticID(id)}_{property}");
        }

        private string GetHelpLinkURI(int id) => $"{BaseRuleDocsURI}/{GetDiagnosticID(id)}.md";
        private string GetDiagnosticID(int id) => $"{DiagnosticIDPrefix}{id:0000}";

        private LocalizableString GetTitle(int id) => GetResourceString(id, "Title");
        private LocalizableString GetMessageFormat(int id) => GetResourceString(id, "MessageFormat");
        private LocalizableString GetDescription(int id) => GetResourceString(id, "Description");
        #endregion
    }
}
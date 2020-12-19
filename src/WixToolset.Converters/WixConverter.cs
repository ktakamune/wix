// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Converters
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.Xml.Linq;
    using WixToolset.Data;
    using WixToolset.Extensibility.Services;

    /// <summary>
    /// WiX source code converter.
    /// </summary>
    public sealed class WixConverter
    {
        private enum ConvertOperation
        {
            Convert,
            Format,
        }

        private static readonly Regex AddPrefix = new Regex(@"^[^a-zA-Z_]", RegexOptions.Compiled);
        private static readonly Regex IllegalIdentifierCharacters = new Regex(@"[^A-Za-z0-9_\.]|\.{2,}", RegexOptions.Compiled); // non 'words' and assorted valid characters

        private const char XDocumentNewLine = '\n'; // XDocument normalizes "\r\n" to just "\n".
        private static readonly XNamespace WixNamespace = "http://wixtoolset.org/schemas/v4/wxs";
        private static readonly XNamespace Wix3Namespace = "http://schemas.microsoft.com/wix/2006/wi";
        private static readonly XNamespace WixBalNamespace = "http://wixtoolset.org/schemas/v4/wxs/bal";
        private static readonly XNamespace WixUtilNamespace = "http://wixtoolset.org/schemas/v4/wxs/util";
        private static readonly XNamespace WixFirewallNamespace = "http://wixtoolset.org/schemas/v4/wxs/firewall";

        private static readonly XName AdminExecuteSequenceElementName = WixNamespace + "AdminExecuteSequence";
        private static readonly XName AdminUISequenceSequenceElementName = WixNamespace + "AdminUISequence";
        private static readonly XName AdvertiseExecuteSequenceElementName = WixNamespace + "AdvertiseExecuteSequence";
        private static readonly XName InstallExecuteSequenceElementName = WixNamespace + "InstallExecuteSequence";
        private static readonly XName InstallUISequenceSequenceElementName = WixNamespace + "InstallUISequence";
        private static readonly XName BootstrapperApplicationElementName = WixNamespace + "BootstrapperApplication";
        private static readonly XName BootstrapperApplicationDllElementName = WixNamespace + "BootstrapperApplicationDll";
        private static readonly XName BootstrapperApplicationRefElementName = WixNamespace + "BootstrapperApplicationRef";
        private static readonly XName EmbeddedChainerElementName = WixNamespace + "EmbeddedChainer";
        private static readonly XName ColumnElementName = WixNamespace + "Column";
        private static readonly XName ComponentElementName = WixNamespace + "Component";
        private static readonly XName ControlElementName = WixNamespace + "Control";
        private static readonly XName ConditionElementName = WixNamespace + "Condition";
        private static readonly XName CreateFolderElementName = WixNamespace + "CreateFolder";
        private static readonly XName CustomTableElementName = WixNamespace + "CustomTable";
        private static readonly XName DataElementName = WixNamespace + "Data";
        private static readonly XName DirectoryElementName = WixNamespace + "Directory";
        private static readonly XName ErrorElementName = WixNamespace + "Error";
        private static readonly XName FeatureElementName = WixNamespace + "Feature";
        private static readonly XName FileElementName = WixNamespace + "File";
        private static readonly XName FragmentElementName = WixNamespace + "Fragment";
        private static readonly XName FirewallRemoteAddressElementName = WixFirewallNamespace + "RemoteAddress";
        private static readonly XName LaunchElementName = WixNamespace + "Launch";
        private static readonly XName LevelElementName = WixNamespace + "Level";
        private static readonly XName ExePackageElementName = WixNamespace + "ExePackage";
        private static readonly XName ModuleElementName = WixNamespace + "Module";
        private static readonly XName MsiPackageElementName = WixNamespace + "MsiPackage";
        private static readonly XName MspPackageElementName = WixNamespace + "MspPackage";
        private static readonly XName MsuPackageElementName = WixNamespace + "MsuPackage";
        private static readonly XName PackageElementName = WixNamespace + "Package";
        private static readonly XName PayloadElementName = WixNamespace + "Payload";
        private static readonly XName PermissionExElementName = WixNamespace + "PermissionEx";
        private static readonly XName ProductElementName = WixNamespace + "Product";
        private static readonly XName ProgressTextElementName = WixNamespace + "ProgressText";
        private static readonly XName PublishElementName = WixNamespace + "Publish";
        private static readonly XName MultiStringValueElementName = WixNamespace + "MultiStringValue";
        private static readonly XName RequiredPrivilegeElementName = WixNamespace + "RequiredPrivilege";
        private static readonly XName ServiceArgumentElementName = WixNamespace + "ServiceArgument";
        private static readonly XName SetDirectoryElementName = WixNamespace + "SetDirectory";
        private static readonly XName SetPropertyElementName = WixNamespace + "SetProperty";
        private static readonly XName ShortcutPropertyElementName = WixNamespace + "ShortcutProperty";
        private static readonly XName TextElementName = WixNamespace + "Text";
        private static readonly XName UITextElementName = WixNamespace + "UIText";
        private static readonly XName VariableElementName = WixNamespace + "Variable";
        private static readonly XName VerbElementName = WixNamespace + "Verb";
        private static readonly XName BalUseUILanguagesName = WixBalNamespace + "UseUILanguages";
        private static readonly XName BalStandardBootstrapperApplicationName = WixBalNamespace + "WixStandardBootstrapperApplication";
        private static readonly XName BalManagedBootstrapperApplicationHostName = WixBalNamespace + "WixManagedBootstrapperApplicationHost";
        private static readonly XName BalOldDotNetCoreBootstrapperApplicationName = WixBalNamespace + "WixDotNetCoreBootstrapperApplication";
        private static readonly XName BalNewDotNetCoreBootstrapperApplicationName = WixBalNamespace + "WixDotNetCoreBootstrapperApplicationHost";
        private static readonly XName UtilCloseApplicationElementName = WixUtilNamespace + "CloseApplication";
        private static readonly XName UtilPermissionExElementName = WixUtilNamespace + "PermissionEx";
        private static readonly XName UtilRegistrySearchName = WixUtilNamespace + "RegistrySearch";
        private static readonly XName UtilXmlConfigElementName = WixUtilNamespace + "XmlConfig";
        private static readonly XName CustomActionElementName = WixNamespace + "CustomAction";
        private static readonly XName PropertyElementName = WixNamespace + "Property";
        private static readonly XName Wix4ElementName = WixNamespace + "Wix";
        private static readonly XName Wix3ElementName = Wix3Namespace + "Wix";
        private static readonly XName WixElementWithoutNamespaceName = XNamespace.None + "Wix";
        private static readonly XName Include4ElementName = WixNamespace + "Include";
        private static readonly XName Include3ElementName = Wix3Namespace + "Include";
        private static readonly XName IncludeElementWithoutNamespaceName = XNamespace.None + "Include";
        private static readonly XName SummaryInformationElementName = WixNamespace + "SummaryInformation";
        private static readonly XName MediaTemplateElementName = WixNamespace + "MediaTemplate";

        private static readonly Dictionary<string, XNamespace> OldToNewNamespaceMapping = new Dictionary<string, XNamespace>()
        {
            { "http://schemas.microsoft.com/wix/BalExtension", "http://wixtoolset.org/schemas/v4/wxs/bal" },
            { "http://schemas.microsoft.com/wix/ComPlusExtension", "http://wixtoolset.org/schemas/v4/wxs/complus" },
            { "http://schemas.microsoft.com/wix/DependencyExtension", "http://wixtoolset.org/schemas/v4/wxs/dependency" },
            { "http://schemas.microsoft.com/wix/DifxAppExtension", "http://wixtoolset.org/schemas/v4/wxs/difxapp" },
            { "http://schemas.microsoft.com/wix/FirewallExtension", "http://wixtoolset.org/schemas/v4/wxs/firewall" },
            { "http://schemas.microsoft.com/wix/HttpExtension", "http://wixtoolset.org/schemas/v4/wxs/http" },
            { "http://schemas.microsoft.com/wix/IIsExtension", "http://wixtoolset.org/schemas/v4/wxs/iis" },
            { "http://schemas.microsoft.com/wix/MsmqExtension", "http://wixtoolset.org/schemas/v4/wxs/msmq" },
            { "http://schemas.microsoft.com/wix/NetFxExtension", "http://wixtoolset.org/schemas/v4/wxs/netfx" },
            { "http://schemas.microsoft.com/wix/PSExtension", "http://wixtoolset.org/schemas/v4/wxs/powershell" },
            { "http://schemas.microsoft.com/wix/SqlExtension", "http://wixtoolset.org/schemas/v4/wxs/sql" },
            { "http://schemas.microsoft.com/wix/TagExtension", "http://wixtoolset.org/schemas/v4/wxs/tag" },
            { "http://schemas.microsoft.com/wix/UtilExtension", WixUtilNamespace },
            { "http://schemas.microsoft.com/wix/VSExtension", "http://wixtoolset.org/schemas/v4/wxs/vs" },
            { "http://wixtoolset.org/schemas/thmutil/2010", "http://wixtoolset.org/schemas/v4/thmutil" },
            { "http://schemas.microsoft.com/wix/2009/Lux", "http://wixtoolset.org/schemas/v4/lux" },
            { "http://schemas.microsoft.com/wix/2006/wi", "http://wixtoolset.org/schemas/v4/wxs" },
            { "http://schemas.microsoft.com/wix/2006/localization", "http://wixtoolset.org/schemas/v4/wxl" },
            { "http://schemas.microsoft.com/wix/2006/libraries", "http://wixtoolset.org/schemas/v4/wixlib" },
            { "http://schemas.microsoft.com/wix/2006/objects", "http://wixtoolset.org/schemas/v4/wixobj" },
            { "http://schemas.microsoft.com/wix/2006/outputs", "http://wixtoolset.org/schemas/v4/wixout" },
            { "http://schemas.microsoft.com/wix/2007/pdbs", "http://wixtoolset.org/schemas/v4/wixpdb" },
            { "http://schemas.microsoft.com/wix/2003/04/actions", "http://wixtoolset.org/schemas/v4/wi/actions" },
            { "http://schemas.microsoft.com/wix/2006/tables", "http://wixtoolset.org/schemas/v4/wi/tables" },
            { "http://schemas.microsoft.com/wix/2006/WixUnit", "http://wixtoolset.org/schemas/v4/wixunit" },
        };

        private readonly Dictionary<XName, Action<XElement>> ConvertElementMapping;

        /// <summary>
        /// Instantiate a new Converter class.
        /// </summary>
        /// <param name="messaging"></param>
        /// <param name="indentationAmount">Indentation value to use when validating leading whitespace.</param>
        /// <param name="errorsAsWarnings">Test errors to display as warnings.</param>
        /// <param name="ignoreErrors">Test errors to ignore.</param>
        public WixConverter(IMessaging messaging, int indentationAmount, IEnumerable<string> errorsAsWarnings = null, IEnumerable<string> ignoreErrors = null)
        {
            this.ConvertElementMapping = new Dictionary<XName, Action<XElement>>
            {
                { WixConverter.AdminExecuteSequenceElementName, this.ConvertSequenceElement },
                { WixConverter.AdminUISequenceSequenceElementName, this.ConvertSequenceElement },
                { WixConverter.AdvertiseExecuteSequenceElementName, this.ConvertSequenceElement },
                { WixConverter.InstallUISequenceSequenceElementName, this.ConvertSequenceElement },
                { WixConverter.InstallExecuteSequenceElementName, this.ConvertSequenceElement },
                { WixConverter.BootstrapperApplicationElementName, this.ConvertBootstrapperApplicationElement },
                { WixConverter.BootstrapperApplicationRefElementName, this.ConvertBootstrapperApplicationRefElement },
                { WixConverter.ColumnElementName, this.ConvertColumnElement },
                { WixConverter.CustomTableElementName, this.ConvertCustomTableElement },
                { WixConverter.ControlElementName, this.ConvertControlElement },
                { WixConverter.ComponentElementName, this.ConvertComponentElement },
                { WixConverter.DataElementName, this.ConvertDataElement },
                { WixConverter.DirectoryElementName, this.ConvertDirectoryElement },
                { WixConverter.FeatureElementName, this.ConvertFeatureElement },
                { WixConverter.FileElementName, this.ConvertFileElement },
                { WixConverter.FragmentElementName, this.ConvertFragmentElement },
                { WixConverter.FirewallRemoteAddressElementName, this.ConvertFirewallRemoteAddressElement },
                { WixConverter.EmbeddedChainerElementName, this.ConvertEmbeddedChainerElement },
                { WixConverter.ErrorElementName, this.ConvertErrorElement },
                { WixConverter.ExePackageElementName, this.ConvertSuppressSignatureValidation },
                { WixConverter.ModuleElementName, this.ConvertModuleElement },
                { WixConverter.MsiPackageElementName, this.ConvertWindowsInstallerPackageElement },
                { WixConverter.MspPackageElementName, this.ConvertWindowsInstallerPackageElement },
                { WixConverter.MsuPackageElementName, this.ConvertSuppressSignatureValidation },
                { WixConverter.PayloadElementName, this.ConvertSuppressSignatureValidation },
                { WixConverter.PermissionExElementName, this.ConvertPermissionExElement },
                { WixConverter.ProductElementName, this.ConvertProductElement },
                { WixConverter.ProgressTextElementName, this.ConvertProgressTextElement },
                { WixConverter.PublishElementName, this.ConvertPublishElement },
                { WixConverter.MultiStringValueElementName, this.ConvertMultiStringValueElement },
                { WixConverter.RequiredPrivilegeElementName, this.ConvertRequiredPrivilegeElement },
                { WixConverter.CustomActionElementName, this.ConvertCustomActionElement },
                { WixConverter.ServiceArgumentElementName, this.ConvertServiceArgumentElement },
                { WixConverter.SetDirectoryElementName, this.ConvertSetDirectoryElement },
                { WixConverter.SetPropertyElementName, this.ConvertSetPropertyElement },
                { WixConverter.ShortcutPropertyElementName, this.ConvertShortcutPropertyElement },
                { WixConverter.TextElementName, this.ConvertTextElement },
                { WixConverter.UITextElementName, this.ConvertUITextElement },
                { WixConverter.VariableElementName, this.ConvertVariableElement },
                { WixConverter.UtilCloseApplicationElementName, this.ConvertUtilCloseApplicationElementName },
                { WixConverter.UtilPermissionExElementName, this.ConvertUtilPermissionExElement },
                { WixConverter.UtilRegistrySearchName, this.ConvertUtilRegistrySearchElement },
                { WixConverter.UtilXmlConfigElementName, this.ConvertUtilXmlConfigElement },
                { WixConverter.PropertyElementName, this.ConvertPropertyElement },
                { WixConverter.WixElementWithoutNamespaceName, this.ConvertElementWithoutNamespace },
                { WixConverter.IncludeElementWithoutNamespaceName, this.ConvertElementWithoutNamespace },
                { WixConverter.VerbElementName, this.ConvertVerbElement },
            };

            this.Messaging = messaging;

            this.IndentationAmount = indentationAmount;

            this.ErrorsAsWarnings = new HashSet<ConverterTestType>(this.YieldConverterTypes(errorsAsWarnings));

            this.IgnoreErrors = new HashSet<ConverterTestType>(this.YieldConverterTypes(ignoreErrors));
        }

        private int Errors { get; set; }

        private HashSet<ConverterTestType> ErrorsAsWarnings { get; set; }

        private HashSet<ConverterTestType> IgnoreErrors { get; set; }

        private IMessaging Messaging { get; }

        private int IndentationAmount { get; set; }

        private ConvertOperation Operation { get; set; }

        private string SourceFile { get; set; }

        private int SourceVersion { get; set; }

        /// <summary>
        /// Convert a file.
        /// </summary>
        /// <param name="sourceFile">The file to convert.</param>
        /// <param name="saveConvertedFile">Option to save the converted errors that are found.</param>
        /// <returns>The number of errors found.</returns>
        public int ConvertFile(string sourceFile, bool saveConvertedFile)
        {
            var document = this.OpenSourceFile(sourceFile);

            if (document is null)
            {
                return 1;
            }

            this.ConvertDocument(document);

            // Fix errors if requested and necessary.
            if (saveConvertedFile && 0 < this.Errors)
            {
                this.SaveDocument(document);
            }

            return this.Errors;
        }

        /// <summary>
        /// Convert a document.
        /// </summary>
        /// <param name="document">The document to convert.</param>
        /// <returns>The number of errors found.</returns>
        public int ConvertDocument(XDocument document)
        {
            // Reset the instance info.
            this.Errors = 0;
            this.SourceVersion = 0;
            this.Operation = ConvertOperation.Convert;

            // Remove the declaration.
            if (null != document.Declaration
                && this.OnError(ConverterTestType.DeclarationPresent, null, "This file contains an XML declaration on the first line."))
            {
                document.Declaration = null;
                TrimLeadingText(document);
            }

            // Start converting the nodes at the top.
            this.ConvertNodes(document.Nodes(), 0);

            return this.Errors;
        }

        /// <summary>
        /// Format a file.
        /// </summary>
        /// <param name="sourceFile">The file to format.</param>
        /// <param name="saveConvertedFile">Option to save the format errors that are found.</param>
        /// <returns>The number of errors found.</returns>
        public int FormatFile(string sourceFile, bool saveConvertedFile)
        {
            var document = this.OpenSourceFile(sourceFile);

            if (document is null)
            {
                return 1;
            }

            this.FormatDocument(document);

            // Fix errors if requested and necessary.
            if (saveConvertedFile && 0 < this.Errors)
            {
                this.SaveDocument(document);
            }

            return this.Errors;
        }

        /// <summary>
        /// Format a document.
        /// </summary>
        /// <param name="document">The document to format.</param>
        /// <returns>The number of errors found.</returns>
        public int FormatDocument(XDocument document)
        {
            // Reset the instance info.
            this.Errors = 0;
            this.SourceVersion = 0;
            this.Operation = ConvertOperation.Format;

            // Remove the declaration.
            if (null != document.Declaration
                && this.OnError(ConverterTestType.DeclarationPresent, null, "This file contains an XML declaration on the first line."))
            {
                document.Declaration = null;
                TrimLeadingText(document);
            }

            // Start converting the nodes at the top.
            this.ConvertNodes(document.Nodes(), 0);

            return this.Errors;
        }

        private XDocument OpenSourceFile(string sourceFile)
        {
            this.SourceFile = sourceFile;

            try
            {
                return XDocument.Load(this.SourceFile, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            }
            catch (XmlException e)
            {
                this.OnError(ConverterTestType.XmlException, null, "The xml is invalid.  Detail: '{0}'", e.Message);
            }

            return null;
        }

        private void SaveDocument(XDocument document)
        {
            var ignoreDeclarationError = this.IgnoreErrors.Contains(ConverterTestType.DeclarationPresent);

            try
            {
                using (var writer = XmlWriter.Create(this.SourceFile, new XmlWriterSettings { OmitXmlDeclaration = !ignoreDeclarationError }))
                {
                    document.Save(writer);
                }
            }
            catch (UnauthorizedAccessException)
            {
                this.OnError(ConverterTestType.UnauthorizedAccessException, null, "Could not write to file.");
            }
        }

        private void ConvertNodes(IEnumerable<XNode> nodes, int level)
        {
            // Note we operate on a copy of the node list since we may
            // remove some whitespace nodes during this processing.
            foreach (var node in nodes.ToList())
            {
                if (node is XText text)
                {
                    if (!String.IsNullOrWhiteSpace(text.Value))
                    {
                        text.Value = text.Value.Trim();
                    }
                    else if (node.NextNode is XCData cdata)
                    {
                        this.EnsurePrecedingWhitespaceRemoved(text, node, ConverterTestType.WhitespacePrecedingNodeWrong);
                    }
                    else if (node.NextNode is XElement element)
                    {
                        this.EnsurePrecedingWhitespaceCorrect(text, node, level, ConverterTestType.WhitespacePrecedingNodeWrong);
                    }
                    else if (node.NextNode is null) // this is the space before the close element
                    {
                        if (node.PreviousNode is null || node.PreviousNode is XCData)
                        {
                            this.EnsurePrecedingWhitespaceRemoved(text, node.Parent, ConverterTestType.WhitespacePrecedingEndElementWrong);
                        }
                        else if (level == 0) // root element's close tag
                        {
                            this.EnsurePrecedingWhitespaceCorrect(text, node, 0, ConverterTestType.WhitespacePrecedingEndElementWrong);
                        }
                        else
                        {
                            this.EnsurePrecedingWhitespaceCorrect(text, node, level - 1, ConverterTestType.WhitespacePrecedingEndElementWrong);
                        }
                    }
                }
                else if (node is XElement element)
                {
                    this.ConvertElement(element);

                    this.ConvertNodes(element.Nodes(), level + 1);
                }
            }
        }

        private void EnsurePrecedingWhitespaceCorrect(XText whitespace, XNode node, int level, ConverterTestType testType)
        {
            if (!WixConverter.LeadingWhitespaceValid(this.IndentationAmount, level, whitespace.Value))
            {
                var message = testType == ConverterTestType.WhitespacePrecedingEndElementWrong ? "The whitespace preceding this end element is incorrect." : "The whitespace preceding this node is incorrect.";

                if (this.OnError(testType, node, message))
                {
                    WixConverter.FixupWhitespace(this.IndentationAmount, level, whitespace);
                }
            }
        }

        private void EnsurePrecedingWhitespaceRemoved(XText whitespace, XNode node, ConverterTestType testType)
        {
            if (!String.IsNullOrEmpty(whitespace.Value) && whitespace.NodeType != XmlNodeType.CDATA)
            {
                var message = testType == ConverterTestType.WhitespacePrecedingEndElementWrong ? "The whitespace preceding this end element is incorrect." : "The whitespace preceding this node is incorrect.";

                if (this.OnError(testType, node, message))
                {
                    whitespace.Remove();
                }
            }
        }

        private void ConvertElement(XElement element)
        {
            // Gather any deprecated namespaces, then update this element tree based on those deprecations.
            var deprecatedToUpdatedNamespaces = new Dictionary<XNamespace, XNamespace>();

            foreach (var declaration in element.Attributes().Where(a => a.IsNamespaceDeclaration))
            {
                if (element.Name == Wix3ElementName || element.Name == Include3ElementName)
                {
                    this.SourceVersion = 3;
                }
                else if (element.Name == Wix4ElementName || element.Name == Include4ElementName)
                {
                    this.SourceVersion = 4;
                }

                if (WixConverter.OldToNewNamespaceMapping.TryGetValue(declaration.Value, out var ns))
                {
                    if (this.OnError(ConverterTestType.XmlnsValueWrong, declaration, "The namespace '{0}' is out of date.  It must be '{1}'.", declaration.Value, ns.NamespaceName))
                    {
                        deprecatedToUpdatedNamespaces.Add(declaration.Value, ns);
                    }
                }
            }

            if (deprecatedToUpdatedNamespaces.Any())
            {
                WixConverter.UpdateElementsWithDeprecatedNamespaces(element.DescendantsAndSelf(), deprecatedToUpdatedNamespaces);
            }

            // Apply any specialized conversion actions.
            if (this.ConvertElementMapping.TryGetValue(element.Name, out var convert))
            {
                convert(element);
            }
        }

        private void ConvertBootstrapperApplicationElement(XElement element)
        {
            var xUseUILanguages = element.Attribute(BalUseUILanguagesName);
            if (xUseUILanguages != null &&
                this.OnError(ConverterTestType.BalUseUILanguagesDeprecated, element, "bal:UseUILanguages is deprecated, 'true' is now the standard behavior."))
            {
                xUseUILanguages.Remove();
            }

            var xBADll = element.Elements(BootstrapperApplicationDllElementName).FirstOrDefault();
            if (xBADll == null)
            {
                xBADll = this.CreateBootstrapperApplicationDllElement(element);

                if (xBADll != null)
                {
                    element.Add(Environment.NewLine);
                    element.Add(xBADll);
                    element.Add(Environment.NewLine);
                }
            }
        }

        private XElement CreateBootstrapperApplicationDllElement(XElement element)
        {
            XElement xBADll = null;
            var xSource = element.Attribute("SourceFile");
            var xDpiAwareness = element.Attribute("DpiAwareness");

            if (xSource != null)
            {
                if (xBADll != null || CreateBADllElement(element, out xBADll))
                {
                    MoveAttribute(element, "SourceFile", xBADll);
                    MoveAttribute(element, "Name", xBADll);
                }
            }
            else if (xDpiAwareness != null || this.SourceVersion < 4) // older code might be relying on old behavior of first Payload element being the BA dll.
            {
                var xFirstChild = element.Elements().FirstOrDefault();
                if (xFirstChild?.Name == PayloadElementName)
                {
                    if (xBADll != null || CreateBADllElement(element, out xBADll))
                    {
                        var attributes = xFirstChild.Attributes().ToList();
                        xFirstChild.Remove();

                        foreach (var attribute in attributes)
                        {
                            xBADll.Add(attribute);
                        }
                    }
                }
                else
                {
                    this.OnError(ConverterTestType.BootstrapperApplicationDllRequired, element, "The new BootstrapperApplicationDll element is required but could not be added automatically since the bootstrapper application dll was not directly specified.");
                }
            }

            if (xDpiAwareness != null)
            {
                if (xBADll != null || CreateBADllElement(element, out xBADll))
                {
                    MoveAttribute(element, "DpiAwareness", xBADll);
                }
            }
            else if (this.SourceVersion < 4 && xBADll != null &&
                this.OnError(ConverterTestType.AssignBootstrapperApplicationDpiAwareness, element, "The BootstrapperApplicationDll DpiAwareness attribute is being set to 'unaware' to ensure it remains the same as the v3 default"))
            {
                xBADll.Add(new XAttribute("DpiAwareness", "unaware"));
            }

            return xBADll;

            bool CreateBADllElement(XObject node, out XElement xCreatedBADll)
            {
                var create = this.OnError(ConverterTestType.BootstrapperApplicationDll, node, "The bootstrapper application dll is now specified in the BootstrapperApplicationDll element.");
                xCreatedBADll = create ? new XElement(BootstrapperApplicationDllElementName) : null;
                return create;
            }
        }

        private void ConvertBootstrapperApplicationRefElement(XElement element)
        {
            var xUseUILanguages = element.Attribute(BalUseUILanguagesName);
            if (xUseUILanguages != null &&
                this.OnError(ConverterTestType.BalUseUILanguagesDeprecated, element, "bal:UseUILanguages is deprecated, 'true' is now the standard behavior."))
            {
                xUseUILanguages.Remove();
            }

            var xId = element.Attribute("Id");
            if (xId != null)
            {
                XName balBAName = null;
                XName oldBalBAName = null;
                string theme = null;

                switch (xId.Value)
                {
                    case "WixStandardBootstrapperApplication.RtfLicense":
                        balBAName = BalStandardBootstrapperApplicationName;
                        theme = "rtfLicense";
                        break;
                    case "WixStandardBootstrapperApplication.RtfLargeLicense":
                        balBAName = BalStandardBootstrapperApplicationName;
                        theme = "rtfLargeLicense";
                        break;
                    case "WixStandardBootstrapperApplication.HyperlinkLicense":
                        balBAName = BalStandardBootstrapperApplicationName;
                        theme = "hyperlinkLicense";
                        break;
                    case "WixStandardBootstrapperApplication.HyperlinkLargeLicense":
                        balBAName = BalStandardBootstrapperApplicationName;
                        theme = "hyperlinkLargeLicense";
                        break;
                    case "WixStandardBootstrapperApplication.HyperlinkSidebarLicense":
                        balBAName = BalStandardBootstrapperApplicationName;
                        theme = "hyperlinkSidebarLicense";
                        break;
                    case "WixStandardBootstrapperApplication.Foundation":
                        balBAName = BalStandardBootstrapperApplicationName;
                        theme = "none";
                        break;
                    case "ManagedBootstrapperApplicationHost":
                    case "ManagedBootstrapperApplicationHost.RtfLicense":
                        balBAName = BalManagedBootstrapperApplicationHostName;
                        theme = "standard";
                        break;
                    case "ManagedBootstrapperApplicationHost.Minimal":
                    case "ManagedBootstrapperApplicationHost.RtfLicense.Minimal":
                    case "ManagedBootstrapperApplicationHost.Foundation":
                        balBAName = BalManagedBootstrapperApplicationHostName;
                        theme = "none";
                        break;
                    case "DotNetCoreBootstrapperApplicationHost":
                    case "DotNetCoreBootstrapperApplicationHost.RtfLicense":
                        balBAName = BalNewDotNetCoreBootstrapperApplicationName;
                        oldBalBAName = BalOldDotNetCoreBootstrapperApplicationName;
                        theme = "standard";
                        break;
                    case "DotNetCoreBootstrapperApplicationHost.Minimal":
                    case "DotNetCoreBootstrapperApplicationHost.RtfLicense.Minimal":
                    case "DotNetCoreBootstrapperApplicationHost.Foundation":
                        balBAName = BalNewDotNetCoreBootstrapperApplicationName;
                        oldBalBAName = BalOldDotNetCoreBootstrapperApplicationName;
                        theme = "none";
                        break;
                }

                if (balBAName != null && theme != null &&
                    this.OnError(ConverterTestType.BalBootstrapperApplicationRefToElement, element, "Built-in bootstrapper applications must be referenced through their custom element"))
                {
                    element.Name = BootstrapperApplicationElementName;
                    xId.Remove();
                    this.ConvertBalBootstrapperApplicationRef(element, theme, balBAName, oldBalBAName);
                }
            }
        }

        private void ConvertBalBootstrapperApplicationRef(XElement element, string theme, XName balBAElementName, XName oldBalBAElementName = null)
        {
            var xBalBa = element.Element(oldBalBAElementName ?? balBAElementName);
            if (xBalBa == null)
            {
                xBalBa = new XElement(balBAElementName);
                element.Add(Environment.NewLine);
                element.Add(xBalBa);
                element.Add(Environment.NewLine);
            }
            else if (oldBalBAElementName != null)
            {
                xBalBa.Name = BalNewDotNetCoreBootstrapperApplicationName;
            }

            if (theme != "standard")
            {
                xBalBa.Add(new XAttribute("Theme", theme));
            }
        }

        private void ConvertColumnElement(XElement element)
        {
            var category = element.Attribute("Category");
            if (category != null)
            {
                var camelCaseValue = LowercaseFirstChar(category.Value);
                if (category.Value != camelCaseValue &&
                    this.OnError(ConverterTestType.ColumnCategoryCamelCase, element, "The CustomTable Category attribute contains an incorrectly cased '{0}' value. Lowercase the first character instead.", category.Name))
                {
                    category.Value = camelCaseValue;
                }
            }

            var modularization = element.Attribute("Modularize");
            if (modularization != null)
            {
                var camelCaseValue = LowercaseFirstChar(modularization.Value);
                if (modularization.Value != camelCaseValue &&
                    this.OnError(ConverterTestType.ColumnModularizeCamelCase, element, "The CustomTable Modularize attribute contains an incorrectly cased '{0}' value. Lowercase the first character instead.", modularization.Name))
                {
                    modularization.Value = camelCaseValue;
                }
            }
        }

        private void ConvertCustomTableElement(XElement element)
        {
            var bootstrapperApplicationData = element.Attribute("BootstrapperApplicationData");
            if (bootstrapperApplicationData != null
                && this.OnError(ConverterTestType.BootstrapperApplicationDataDeprecated, element, "The CustomTable element contains deprecated '{0}' attribute. Use the 'Unreal' attribute instead.", bootstrapperApplicationData.Name))
            {
                element.Add(new XAttribute("Unreal", bootstrapperApplicationData.Value));
                bootstrapperApplicationData.Remove();
            }
        }

        private void ConvertControlElement(XElement element)
        {
            var remove = new List<XElement>();

            foreach (var xCondition in element.Elements(ConditionElementName))
            {
                var action = UppercaseFirstChar(xCondition.Attribute("Action")?.Value);
                if (!String.IsNullOrEmpty(action) &&
                    TryGetInnerText(xCondition, out var text) &&
                    this.OnError(ConverterTestType.InnerTextDeprecated, element, "Using {0} element text is deprecated. Use the '{1}Condition' attribute instead.", xCondition.Name.LocalName, action))
                {
                    element.Add(new XAttribute(action + "Condition", text));
                    remove.Add(xCondition);
                }
            }

            for (var i = remove.Count - 1; i >= 0; i--)
            {
                remove[i].Remove();
            }
        }

        private void ConvertComponentElement(XElement element)
        {
            var guid = element.Attribute("Guid");
            if (guid != null && guid.Value == "*")
            {
                if (this.OnError(ConverterTestType.AutoGuidUnnecessary, element, "Using '*' for the Component Guid attribute is unnecessary. Remove the attribute to remove the redundancy."))
                {
                    guid.Remove();
                }
            }

            var xCondition = element.Element(ConditionElementName);
            if (xCondition != null)
            {
                if (TryGetInnerText(xCondition, out var text) &&
                    this.OnError(ConverterTestType.InnerTextDeprecated, element, "Using {0} element text is deprecated. Use the 'Condition' attribute instead.", xCondition.Name.LocalName))
                {
                    element.Add(new XAttribute("Condition", text));
                    xCondition.Remove();
                }
            }
        }

        private void ConvertDirectoryElement(XElement element)
        {
            if (null == element.Attribute("Name"))
            {
                var attribute = element.Attribute("ShortName");
                if (null != attribute)
                {
                    var shortName = attribute.Value;
                    if (this.OnError(ConverterTestType.AssignDirectoryNameFromShortName, element, "The directory ShortName attribute is being renamed to Name since Name wasn't specified for value '{0}'", shortName))
                    {
                        element.Add(new XAttribute("Name", shortName));
                        attribute.Remove();
                    }
                }
            }
        }

        private void ConvertFeatureElement(XElement element)
        {
            var xAbsent = element.Attribute("Absent");
            if (xAbsent != null &&
                this.OnError(ConverterTestType.FeatureAbsentAttributeReplaced, element, "The Feature element's Absent attribute has been replaced with the AllowAbsent attribute. Use the 'AllowAbsent' attribute instead."))
            {
                if (xAbsent.Value == "disallow")
                {
                    element.Add(new XAttribute("AllowAbsent", "no"));
                }
                xAbsent.Remove();
            }

            var xAllowAdvertise = element.Attribute("AllowAdvertise");
            if (xAllowAdvertise != null)
            {
                if ((xAllowAdvertise.Value == "system" || xAllowAdvertise.Value == "allow") &&
                    this.OnError(ConverterTestType.FeatureAllowAdvertiseValueDeprecated, element, "The AllowAdvertise attribute's '{0}' value is deprecated. Set the value to 'yes' instead.", xAllowAdvertise.Value))
                {
                    xAllowAdvertise.Value = "yes";
                }
                else if (xAllowAdvertise.Value == "disallow" &&
                    this.OnError(ConverterTestType.FeatureAllowAdvertiseValueDeprecated, element, "The AllowAdvertise attribute's '{0}' value is deprecated. Remove the value instead.", xAllowAdvertise.Value))
                {
                    xAllowAdvertise.Remove();
                }
            }

            var xCondition = element.Element(ConditionElementName);
            if (xCondition != null)
            {
                var level = xCondition.Attribute("Level")?.Value;
                if (!String.IsNullOrEmpty(level) &&
                    TryGetInnerText(xCondition, out var text) &&
                    this.OnError(ConverterTestType.InnerTextDeprecated, element, "Using {0} element text is deprecated. Use the 'Level' element instead.", xCondition.Name.LocalName))
                {
                    xCondition.AddAfterSelf(new XElement(LevelElementName,
                        new XAttribute("Value", level),
                        new XAttribute("Condition", text)
                        ));
                    xCondition.Remove();
                }
            }
        }

        private void ConvertFileElement(XElement element)
        {
            if (this.SourceVersion < 4 && null == element.Attribute("Id"))
            {
                var attribute = element.Attribute("Name");

                if (null == attribute)
                {
                    attribute = element.Attribute("Source");
                }

                if (null != attribute)
                {
                    var name = Path.GetFileName(attribute.Value);

                    if (this.OnError(ConverterTestType.AssignAnonymousFileId, element, "The file id is being updated to '{0}' to ensure it remains the same as the v3 default", name))
                    {
                        IEnumerable<XAttribute> attributes = element.Attributes().ToList();
                        element.RemoveAttributes();
                        element.Add(new XAttribute("Id", GetIdentifierFromName(name)));
                        element.Add(attributes);
                    }
                }
            }
        }

        private void ConvertFragmentElement(XElement element)
        {
            var remove = new List<XElement>();

            foreach (var xCondition in element.Elements(ConditionElementName))
            {
                var message = xCondition.Attribute("Message")?.Value;

                if (!String.IsNullOrEmpty(message) &&
                    TryGetInnerText(xCondition, out var text) &&
                    this.OnError(ConverterTestType.InnerTextDeprecated, element, "Using {0} element text is deprecated. Use the 'Launch' element instead.", xCondition.Name.LocalName))
                {
                    xCondition.AddAfterSelf(new XElement(LaunchElementName,
                        new XAttribute("Condition", text),
                        new XAttribute("Message", message)
                        ));
                    remove.Add(xCondition);
                }
            }

            for (var i = remove.Count - 1; i >= 0; i--)
            {
                remove[i].Remove();
            }
        }

        private void ConvertFirewallRemoteAddressElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Value");

        private void ConvertEmbeddedChainerElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Condition");

        private void ConvertErrorElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Message");

        private void ConvertPermissionExElement(XElement element)
        {
            var xCondition = element.Element(ConditionElementName);
            if (xCondition != null)
            {
                if (TryGetInnerText(xCondition, out var text) &&
                    this.OnError(ConverterTestType.InnerTextDeprecated, element, "Using {0} element text is deprecated. Use the 'Condition' attribute instead.", xCondition.Name.LocalName))
                {
                    element.Add(new XAttribute("Condition", text));
                    xCondition.Remove();
                }
            }
        }

        private void ConvertProgressTextElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Message");

        private void ConvertModuleElement(XElement element)
        {
            if (element.Attribute("Guid") == null // skip already-converted Module elements
                && this.OnError(ConverterTestType.ModuleAndPackageRenamed, element, "The Module and Package elements have been renamed and reorganized for simplicity."))
            {
                var xModule = element;

                var xSummaryInformation = xModule.Element(PackageElementName);
                if (xSummaryInformation != null)
                {
                    xSummaryInformation.Name = SummaryInformationElementName;

                    var xInstallerVersion = xSummaryInformation.Attribute("InstallerVersion");
                    if (this.SourceVersion < 4 && xInstallerVersion == null)
                    {
                        this.OnError(ConverterTestType.InstallerVersionBehaviorChange, element, "Breaking change: The default value for Package/@InstallerVersion has been changed to '500' regardless of build platform. If you need a lower version, set it manually in the Module element.");
                    }

                    RemoveAttribute(xSummaryInformation, "AdminImage");
                    RemoveAttribute(xSummaryInformation, "Comments");
                    MoveAttribute(xSummaryInformation, "Id", xModule, "Guid");
                    MoveAttribute(xSummaryInformation, "InstallerVersion", xModule);
                    RemoveAttribute(xSummaryInformation, "Languages");
                    RemoveAttribute(xSummaryInformation, "Platform");
                    RemoveAttribute(xSummaryInformation, "Platforms");
                    RemoveAttribute(xSummaryInformation, "ReadOnly");
                    MoveAttribute(xSummaryInformation, "SummaryCodepage", xSummaryInformation, "Codepage", defaultValue: "1252");

                    if (!xSummaryInformation.HasAttributes)
                    {
                        xSummaryInformation.Remove();
                    }
                }
            }
        }

        private void ConvertProductElement(XElement element)
        {
            var id = element.Attribute("Id");
            if (id != null && id.Value == "*")
            {
                if (this.OnError(ConverterTestType.AutoGuidUnnecessary, element, "Using '*' for the Product Id attribute is unnecessary. Remove the attribute to remove the redundancy."))
                {
                    id.Remove();
                }
            }

            var xConditions = element.Elements(ConditionElementName).ToList();
            foreach (var xCondition in xConditions)
            {
                var message = xCondition.Attribute("Message")?.Value;

                if (!String.IsNullOrEmpty(message) &&
                    TryGetInnerText(xCondition, out var text) &&
                    this.OnError(ConverterTestType.InnerTextDeprecated, element, "Using {0} element text is deprecated. Use the 'Launch' element instead.", xCondition.Name.LocalName))
                {
                    xCondition.AddAfterSelf(new XElement(LaunchElementName,
                        new XAttribute("Condition", text),
                        new XAttribute("Message", message)
                        ));
                    xCondition.Remove();
                }
            }

            var xMediaTemplate = element.Element(MediaTemplateElementName);
            if (xMediaTemplate?.HasAttributes == false
                && this.OnError(ConverterTestType.DefaultMediaTemplate, element, "A MediaTemplate with no attributes set is now provided by default. Remove the element."))
            {
                xMediaTemplate.Remove();
            }

            if (this.OnError(ConverterTestType.ProductAndPackageRenamed, element, "The Product and Package elements have been renamed and reorganized for simplicity."))
            {
                var xPackage = element;
                xPackage.Name = PackageElementName;

                var xSummaryInformation = xPackage.Element(PackageElementName);
                if (xSummaryInformation != null)
                {
                    xSummaryInformation.Name = SummaryInformationElementName;

                    var xInstallerVersion = xSummaryInformation.Attribute("InstallerVersion");
                    if (this.SourceVersion < 4 && xInstallerVersion == null)
                    {
                        this.OnError(ConverterTestType.InstallerVersionBehaviorChange, element, "Breaking change: The default value for Package/@InstallerVersion has been changed to '500' regardless of build platform. If you need a lower version, set it manually in the Package element.");
                    }

                    RemoveAttribute(xSummaryInformation, "AdminImage");
                    RemoveAttribute(xSummaryInformation, "Comments");
                    MoveAttribute(xSummaryInformation, "Compressed", xPackage, defaultValue: "no");
                    RemoveAttribute(xSummaryInformation, "Id");
                    MoveAttribute(xSummaryInformation, "InstallerVersion", xPackage, defaultValue: "500");
                    MoveAttribute(xSummaryInformation, "InstallScope", xPackage, "Scope", defaultValue: "perMachine");
                    RemoveAttribute(xSummaryInformation, "Languages");
                    RemoveAttribute(xSummaryInformation, "Platform");
                    RemoveAttribute(xSummaryInformation, "Platforms");
                    RemoveAttribute(xSummaryInformation, "ReadOnly");
                    MoveAttribute(xSummaryInformation, "ShortNames", xPackage);
                    MoveAttribute(xSummaryInformation, "SummaryCodepage", xSummaryInformation, "Codepage", defaultValue: "1252");
                    MoveAttribute(xPackage, "Id", xPackage, "ProductCode");

                    var xInstallPrivileges = xSummaryInformation.Attribute("InstallPrivileges");
                    switch (xInstallPrivileges?.Value)
                    {
                        case "limited":
                            xPackage.SetAttributeValue("Scope", "perUser");
                            break;
                        case "elevated":
                        {
                            var xAllUsers = xPackage.Elements(PropertyElementName).SingleOrDefault(p => p.Attribute("Id")?.Value == "ALLUSERS");
                            if (xAllUsers?.Attribute("Value")?.Value == "1")
                            {
                                xAllUsers?.Remove();
                            }
                        }
                        break;
                    }

                    xInstallPrivileges?.Remove();

                    if (!xSummaryInformation.HasAttributes)
                    {
                        xSummaryInformation.Remove();
                    }
                }
            }
        }

        private static void MoveAttribute(XElement xSource, string attributeName, XElement xDestination, string destinationAttributeName = null, string defaultValue = null)
        {
            var xAttribute = xSource.Attribute(attributeName);
            if (xAttribute != null && (defaultValue == null || xAttribute.Value != defaultValue))
            {
                xDestination.SetAttributeValue(destinationAttributeName ?? attributeName, xAttribute.Value);
            }

            xAttribute?.Remove();
        }

        private static void RemoveAttribute(XElement xSummaryInformation, string attributeName)
        {
            var xAttribute = xSummaryInformation.Attribute(attributeName);
            xAttribute?.Remove();
        }

        private void ConvertPublishElement(XElement element)
        {
            this.ConvertInnerTextToAttribute(element, "Condition");

            var xCondition = element.Attribute("Condition");
            if (xCondition?.Value == "1" &&
                this.OnError(ConverterTestType.PublishConditionOneUnnecessary, element, "Adding Condition='1' on {0} elements is no longer necessary. Remove the Condition attribute.", xCondition.Name.LocalName))
            {
                xCondition.Remove();
            }
        }

        private void ConvertMultiStringValueElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Value");

        private void ConvertRequiredPrivilegeElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Name");

        private void ConvertRowElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Value");

        private void ConvertDataElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Value");

        private void ConvertSequenceElement(XElement element)
        {
            foreach (var child in element.Elements())
            {
                this.ConvertInnerTextToAttribute(child, "Condition");
            }
        }

        private void ConvertServiceArgumentElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Value");

        private void ConvertSetDirectoryElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Condition");

        private void ConvertSetPropertyElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Condition");

        private void ConvertShortcutPropertyElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Value");

        private void ConvertSuppressSignatureValidation(XElement element)
        {
            var suppressSignatureValidation = element.Attribute("SuppressSignatureValidation");

            if (null != suppressSignatureValidation)
            {
                if (this.OnError(ConverterTestType.SuppressSignatureValidationDeprecated, element, "The chain package element contains deprecated '{0}' attribute. Use the 'EnableSignatureValidation' attribute instead.", suppressSignatureValidation.Name))
                {
                    if ("no" == suppressSignatureValidation.Value)
                    {
                        element.Add(new XAttribute("EnableSignatureValidation", "yes"));
                    }
                }

                suppressSignatureValidation.Remove();
            }
        }

        private void ConvertTextElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Value");

        private void ConvertUITextElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Value");

        private void ConvertWindowsInstallerPackageElement(XElement element)
        {
            this.ConvertSuppressSignatureValidation(element);

            if (null != element.Attribute("DisplayInternalUI"))
            {
                this.OnError(ConverterTestType.DisplayInternalUiNotConvertable, element, "The DisplayInternalUI functionality has fundamentally changed and requires BootstrapperApplication support.");
            }
        }

        private void ConvertVerbElement(XElement element)
        {
            if (null != element.Attribute("Target"))
            {
                this.OnError(ConverterTestType.VerbTargetNotConvertable, element, "The Verb/@Target attribute has been replaced with typed @TargetFile and @TargetProperty attributes.");
            }
        }

        private void ConvertCustomActionElement(XElement xCustomAction)
        {
            var xBinaryKey = xCustomAction.Attribute("BinaryKey");
            if (xBinaryKey != null && this.OnError(ConverterTestType.CustomActionKeysAreNowRefs, xCustomAction, "The CustomAction attributes have been renamed from BinaryKey and FileKey to BinaryRef and FileRef."))
            {
                xCustomAction.SetAttributeValue("BinaryRef", xBinaryKey.Value);
                xBinaryKey.Remove();
                xBinaryKey = xCustomAction.Attribute("BinaryRef");
            }

            var xFileKey = xCustomAction.Attribute("FileKey");
            if (xFileKey != null && this.OnError(ConverterTestType.CustomActionKeysAreNowRefs, xCustomAction, "The CustomAction attributes have been renamed from BinaryKey and FileKey to BinaryRef and FileRef."))
            {
                xCustomAction.SetAttributeValue("FileRef", xFileKey.Value);
                xFileKey.Remove();
            }

            if (xBinaryKey?.Value == "WixCA" || xBinaryKey?.Value == "UtilCA")
            {
                if (this.OnError(ConverterTestType.WixCABinaryIdRenamed, xCustomAction, "The WixCA custom action DLL Binary table id has been renamed. Use the id 'Wix4UtilCA_X86' instead."))
                {
                    xBinaryKey.Value = "Wix4UtilCA_X86";
                }
            }

            if (xBinaryKey?.Value == "WixCA_x64" || xBinaryKey?.Value == "UtilCA_x64")
            {
                if (this.OnError(ConverterTestType.WixCABinaryIdRenamed, xCustomAction, "The WixCA_x64 custom action DLL Binary table id has been renamed. Use the id 'Wix4UtilCA_X64' instead."))
                {
                    xBinaryKey.Value = "Wix4UtilCA_X64";
                }
            }

            var xDllEntry = xCustomAction.Attribute("DllEntry");

            if (xDllEntry?.Value == "CAQuietExec" || xDllEntry?.Value == "CAQuietExec64")
            {
                if (this.OnError(ConverterTestType.QuietExecCustomActionsRenamed, xCustomAction, "The CAQuietExec and CAQuietExec64 custom action ids have been renamed. Use the ids 'WixQuietExec' and 'WixQuietExec64' instead."))
                {
                    xDllEntry.Value = xDllEntry.Value.Replace("CAQuietExec", "WixQuietExec");
                }
            }

            var xProperty = xCustomAction.Attribute("Property");

            if (xProperty?.Value == "QtExecCmdLine" || xProperty?.Value == "QtExec64CmdLine")
            {
                if (this.OnError(ConverterTestType.QuietExecCustomActionsRenamed, xCustomAction, "The QtExecCmdLine and QtExec64CmdLine property ids have been renamed. Use the ids 'WixQuietExecCmdLine' and 'WixQuietExec64CmdLine' instead."))
                {
                    xProperty.Value = xProperty.Value.Replace("QtExec", "WixQuietExec");
                }
            }

            var xScript = xCustomAction.Attribute("Script");

            if (xScript != null && TryGetInnerText(xCustomAction, out var scriptText))
            {
                if (this.OnError(ConverterTestType.InnerTextDeprecated, xCustomAction, "Using {0} element text is deprecated. Extract the text to a file and use the 'ScriptSourceFile' attribute to reference it.", xCustomAction.Name.LocalName))
                {
                    var scriptFolder = Path.GetDirectoryName(this.SourceFile) ?? String.Empty;
                    var id = xCustomAction.Attribute("Id")?.Value ?? Guid.NewGuid().ToString("N");
                    var ext = (xScript.Value == "jscript") ? ".js" : (xScript.Value == "vbscript") ? ".vbs" : ".txt";

                    var scriptFile = Path.Combine(scriptFolder, id + ext);
                    File.WriteAllText(scriptFile, scriptText);

                    RemoveChildren(xCustomAction);
                    xCustomAction.Add(new XAttribute("ScriptSourceFile", scriptFile));
                }
            }
        }

        private void ConvertVariableElement(XElement xVariable)
        {
            var xType = xVariable.Attribute("Type");
            var xValue = xVariable.Attribute("Value");
            if (this.SourceVersion < 4)
            {
                if (xType == null)
                {
                    if (WasImplicitlyStringTyped(xValue?.Value) &&
                        this.OnError(ConverterTestType.AssignVariableTypeFormatted, xVariable, "The \"string\" variable type now denotes a literal string. Use \"formatted\" to keep the previous behavior."))
                    {
                        xVariable.Add(new XAttribute("Type", "formatted"));
                    }
                }
                else if (xType.Value == "string" &&
                        this.OnError(ConverterTestType.AssignVariableTypeFormatted, xVariable, "The \"string\" variable type now denotes a literal string. Use \"formatted\" to keep the previous behavior."))
                {
                    xType.Value = "formatted";
                }
            }
        }

        private void ConvertPropertyElement(XElement xProperty)
        {
            var xId = xProperty.Attribute("Id");

            if (xId.Value == "QtExecCmdTimeout")
            {
                this.OnError(ConverterTestType.QtExecCmdTimeoutAmbiguous, xProperty, "QtExecCmdTimeout was previously used for both CAQuietExec and CAQuietExec64. For WixQuietExec, use WixQuietExecCmdTimeout. For WixQuietExec64, use WixQuietExec64CmdTimeout.");
            }

            this.ConvertInnerTextToAttribute(xProperty, "Value");
        }

        private void ConvertUtilCloseApplicationElementName(XElement element) => this.ConvertInnerTextToAttribute(element, "Condition");

        private void ConvertUtilPermissionExElement(XElement element)
        {
            if (this.SourceVersion < 4 && null == element.Attribute("Inheritable"))
            {
                var inheritable = element.Parent.Name == CreateFolderElementName;
                if (!inheritable)
                {
                    if (this.OnError(ConverterTestType.AssignPermissionExInheritable, element, "The PermissionEx Inheritable attribute is being set to 'no' to ensure it remains the same as the v3 default"))
                    {
                        element.Add(new XAttribute("Inheritable", "no"));
                    }
                }
            }
        }

        private void ConvertUtilRegistrySearchElement(XElement element)
        {
            if (this.SourceVersion < 4)
            {
                var result = element.Attribute("Result")?.Value;
                if (result == null || result == "value")
                {
                    this.OnError(ConverterTestType.UtilRegistryValueSearchBehaviorChange, element, "Breaking change: util:RegistrySearch for a value no longer clears the variable when the key or value is missing.");
                }
            }
        }

        private void ConvertUtilXmlConfigElement(XElement element) => this.ConvertInnerTextToAttribute(element, "Value");

        /// <summary>
        /// Converts a Wix element.
        /// </summary>
        /// <param name="element">The Wix element to convert.</param>
        /// <returns>The converted element.</returns>
        private void ConvertElementWithoutNamespace(XElement element)
        {
            if (this.OnError(ConverterTestType.XmlnsMissing, element, "The xmlns attribute is missing.  It must be present with a value of '{0}'.", WixNamespace.NamespaceName))
            {
                element.Name = WixNamespace.GetName(element.Name.LocalName);

                element.Add(new XAttribute("xmlns", WixNamespace.NamespaceName)); // set the default namespace.

                foreach (var elementWithoutNamespace in element.DescendantsAndSelf().Where(e => XNamespace.None == e.Name.Namespace))
                {
                    elementWithoutNamespace.Name = WixNamespace.GetName(elementWithoutNamespace.Name.LocalName);
                }
            }
        }

        private void ConvertInnerTextToAttribute(XElement element, string attributeName)
        {
            if (TryGetInnerText(element, out var text) &&
                this.OnError(ConverterTestType.InnerTextDeprecated, element, "Using {0} element text is deprecated. Use the '{1}' attribute instead.", element.Name.LocalName, attributeName))
            {
                element.Add(new XAttribute(attributeName, text));
                RemoveChildren(element);
            }
        }

        private IEnumerable<ConverterTestType> YieldConverterTypes(IEnumerable<string> types)
        {
            if (null != types)
            {
                foreach (var type in types)
                {
                    if (Enum.TryParse<ConverterTestType>(type, true, out var itt))
                    {
                        yield return itt;
                    }
                    else // not a known ConverterTestType
                    {
                        this.OnError(ConverterTestType.ConverterTestTypeUnknown, null, "Unknown error type: '{0}'.", type);
                    }
                }
            }
        }

        private static void UpdateElementsWithDeprecatedNamespaces(IEnumerable<XElement> elements, Dictionary<XNamespace, XNamespace> deprecatedToUpdatedNamespaces)
        {
            foreach (var element in elements)
            {

                if (deprecatedToUpdatedNamespaces.TryGetValue(element.Name.Namespace, out var ns))
                {
                    element.Name = ns.GetName(element.Name.LocalName);
                }

                // Remove all the attributes and add them back to with their namespace updated (as necessary).
                IEnumerable<XAttribute> attributes = element.Attributes().ToList();
                element.RemoveAttributes();

                foreach (var attribute in attributes)
                {
                    var convertedAttribute = attribute;

                    if (attribute.IsNamespaceDeclaration)
                    {
                        if (deprecatedToUpdatedNamespaces.TryGetValue(attribute.Value, out ns))
                        {
                            convertedAttribute = ("xmlns" == attribute.Name.LocalName) ? new XAttribute(attribute.Name.LocalName, ns.NamespaceName) : new XAttribute(XNamespace.Xmlns + attribute.Name.LocalName, ns.NamespaceName);
                        }
                    }
                    else if (deprecatedToUpdatedNamespaces.TryGetValue(attribute.Name.Namespace, out ns))
                    {
                        convertedAttribute = new XAttribute(ns.GetName(attribute.Name.LocalName), attribute.Value);
                    }

                    element.Add(convertedAttribute);
                }
            }
        }

        /// <summary>
        /// Determine if the whitespace preceding a node is appropriate for its depth level.
        /// </summary>
        /// <param name="indentationAmount">Indentation value to use when validating leading whitespace.</param>
        /// <param name="level">The depth level that should match this whitespace.</param>
        /// <param name="whitespace">The whitespace to validate.</param>
        /// <returns>true if the whitespace is legal; false otherwise.</returns>
        private static bool LeadingWhitespaceValid(int indentationAmount, int level, string whitespace)
        {
            // Strip off leading newlines; there can be an arbitrary number of these.
            whitespace = whitespace.TrimStart(XDocumentNewLine);

            var indentation = new string(' ', level * indentationAmount);

            return whitespace == indentation;
        }

        /// <summary>
        /// Fix the whitespace in a whitespace node.
        /// </summary>
        /// <param name="indentationAmount">Indentation value to use when validating leading whitespace.</param>
        /// <param name="level">The depth level of the desired whitespace.</param>
        /// <param name="whitespace">The whitespace node to fix.</param>
        private static void FixupWhitespace(int indentationAmount, int level, XText whitespace)
        {
            var value = new StringBuilder(whitespace.Value.Length);

            // Keep any previous preceeding new lines.
            var newlines = whitespace.Value.TakeWhile(c => c == XDocumentNewLine).Count();

            // Ensure there is always at least one new line before the indentation.
            value.Append(XDocumentNewLine, newlines == 0 ? 1 : newlines);

            whitespace.Value = value.Append(' ', level * indentationAmount).ToString();
        }

        /// <summary>
        /// Output an error message to the console.
        /// </summary>
        /// <param name="converterTestType">The type of converter test.</param>
        /// <param name="node">The node that caused the error.</param>
        /// <param name="message">Detailed error message.</param>
        /// <param name="args">Additional formatted string arguments.</param>
        /// <returns>Returns true indicating that action should be taken on this error, and false if it should be ignored.</returns>
        private bool OnError(ConverterTestType converterTestType, XObject node, string message, params object[] args)
        {
            // Ignore the error if explicitly ignored or outside the range of the current operation.
            if (this.IgnoreErrors.Contains(converterTestType) ||
                (this.Operation == ConvertOperation.Convert && converterTestType < ConverterTestType.DeclarationPresent) ||
                (this.Operation == ConvertOperation.Format && converterTestType > ConverterTestType.DeclarationPresent))
            {
                return false;
            }

            // Increase the error count.
            this.Errors++;

            var sourceLine = (null == node) ? new SourceLineNumber(this.SourceFile ?? "wix.exe") : new SourceLineNumber(this.SourceFile, ((IXmlLineInfo)node).LineNumber);
            var warning = this.ErrorsAsWarnings.Contains(converterTestType);
            var display = String.Format(CultureInfo.CurrentCulture, message, args);

            var msg = new Message(sourceLine, warning ? MessageLevel.Warning : MessageLevel.Error, (int)converterTestType, "{0} ({1})", display, converterTestType.ToString());

            this.Messaging.Write(msg);

            return true;
        }

        /// <summary>
        /// Return an identifier based on passed file/directory name
        /// </summary>
        /// <param name="name">File/directory name to generate identifer from</param>
        /// <returns>A version of the name that is a legal identifier.</returns>
        /// <remarks>This is duplicated from WiX's Common class.</remarks>
        private static string GetIdentifierFromName(string name)
        {
            var result = IllegalIdentifierCharacters.Replace(name, "_"); // replace illegal characters with "_".

            // MSI identifiers must begin with an alphabetic character or an
            // underscore. Prefix all other values with an underscore.
            if (AddPrefix.IsMatch(name))
            {
                result = String.Concat("_", result);
            }

            return result;
        }

        private static string LowercaseFirstChar(string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                var c = Char.ToLowerInvariant(value[0]);
                if (c != value[0])
                {
                    var remainder = value.Length > 1 ? value.Substring(1) : String.Empty;
                    return c + remainder;
                }
            }

            return value;
        }

        private static string UppercaseFirstChar(string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                var c = Char.ToUpperInvariant(value[0]);
                if (c != value[0])
                {
                    var remainder = value.Length > 1 ? value.Substring(1) : String.Empty;
                    return c + remainder;
                }
            }

            return value;
        }

        private static bool TryGetInnerText(XElement element, out string value)
        {
            value = null;

            var nodes = element.Nodes();

            if (nodes.All(e => e.NodeType == XmlNodeType.Text || e.NodeType == XmlNodeType.CDATA))
            {
                value = String.Join(String.Empty, nodes.Cast<XText>().Select(TrimTextValue));
            }

            return !String.IsNullOrEmpty(value);
        }

        private static bool IsTextNode(XNode node, out XText text)
        {
            text = null;

            if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA)
            {
                text = (XText)node;
            }

            return text != null;
        }

        private static void TrimLeadingText(XDocument document)
        {
            while (IsTextNode(document.Nodes().FirstOrDefault(), out var text))
            {
                text.Remove();
            }
        }

        private static string TrimTextValue(XText text)
        {
            var value = text.Value;

            if (String.IsNullOrEmpty(value))
            {
                return String.Empty;
            }
            else if (text.NodeType == XmlNodeType.CDATA && String.IsNullOrWhiteSpace(value))
            {
                return " ";
            }

            return value.Trim();
        }

        private static void RemoveChildren(XElement element)
        {
            var nodes = element.Nodes().ToList();
            foreach (var node in nodes)
            {
                node.Remove();
            }
        }

        private static bool WasImplicitlyStringTyped(string value)
        {
            if (value == null)
            {
                return false;
            }
            else if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                if (Int32.TryParse(value.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture.NumberFormat, out var _))
                {
                    return false;
                }
                else if (Version.TryParse(value.Substring(1), out var _))
                {
                    return false;
                }
            }
            else if (Int64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out var _))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Converter test types.  These are used to condition error messages down to warnings.
        /// </summary>
        private enum ConverterTestType
        {
            /// <summary>
            /// Internal-only: displayed when a string cannot be converted to an ConverterTestType.
            /// </summary>
            ConverterTestTypeUnknown,

            /// <summary>
            /// Displayed when an XML loading exception has occurred.
            /// </summary>
            XmlException,

            /// <summary>
            /// Displayed when a file cannot be accessed; typically when trying to save back a fixed file.
            /// </summary>
            UnauthorizedAccessException,

            /// <summary>
            /// Displayed when the whitespace preceding a node is wrong.
            /// </summary>
            WhitespacePrecedingNodeWrong,

            /// <summary>
            /// Displayed when the whitespace preceding an end element is wrong.
            /// </summary>
            WhitespacePrecedingEndElementWrong,

            /// <summary>
            /// Displayed when the XML declaration is present in the source file.
            /// </summary>
            DeclarationPresent,

            /// <summary>
            /// Displayed when the xmlns attribute is missing from the document element.
            /// </summary>
            XmlnsMissing,

            /// <summary>
            /// Displayed when the xmlns attribute on the document element is wrong.
            /// </summary>
            XmlnsValueWrong,

            /// <summary>
            /// Assign an identifier to a File element when on Id attribute is specified.
            /// </summary>
            AssignAnonymousFileId,

            /// <summary>
            /// SuppressSignatureValidation attribute is deprecated and replaced with EnableSignatureValidation.
            /// </summary>
            SuppressSignatureValidationDeprecated,

            /// <summary>
            /// WixCA Binary/@Id has been renamed to UtilCA.
            /// </summary>
            WixCABinaryIdRenamed,

            /// <summary>
            /// QtExec custom actions have been renamed.
            /// </summary>
            QuietExecCustomActionsRenamed,

            /// <summary>
            /// QtExecCmdTimeout was previously used for both CAQuietExec and CAQuietExec64. For WixQuietExec, use WixQuietExecCmdTimeout. For WixQuietExec64, use WixQuietExec64CmdTimeout.
            /// </summary>
            QtExecCmdTimeoutAmbiguous,

            /// <summary>
            /// Directory/@ShortName may only be specified with Directory/@Name.
            /// </summary>
            AssignDirectoryNameFromShortName,

            /// <summary>
            /// BootstrapperApplicationData attribute is deprecated and replaced with Unreal.
            /// </summary>
            BootstrapperApplicationDataDeprecated,

            /// <summary>
            /// Inheritable is new and is now defaulted to 'yes' which is a change in behavior for all but children of CreateFolder.
            /// </summary>
            AssignPermissionExInheritable,

            /// <summary>
            /// Column element's Category attribute is camel-case.
            /// </summary>
            ColumnCategoryCamelCase,

            /// <summary>
            /// Column element's Modularize attribute is camel-case.
            /// </summary>
            ColumnModularizeCamelCase,

            /// <summary>
            /// Inner text value should move to an attribute.
            /// </summary>
            InnerTextDeprecated,

            /// <summary>
            /// Explicit auto-GUID unnecessary.
            /// </summary>
            AutoGuidUnnecessary,

            /// <summary>
            /// The Feature Absent attribute renamed to AllowAbsent.
            /// </summary>
            FeatureAbsentAttributeReplaced,

            /// <summary>
            /// The Feature AllowAdvertise attribute value deprecated.
            /// </summary>
            FeatureAllowAdvertiseValueDeprecated,

            /// <summary>
            /// The Condition='1' attribute is unnecessary on Publish elements.
            /// </summary>
            PublishConditionOneUnnecessary,

            /// <summary>
            /// DpiAwareness is new and is defaulted to 'perMonitorV2' which is a change in behavior.
            /// </summary>
            AssignBootstrapperApplicationDpiAwareness,

            /// <summary>
            /// The string variable type was previously treated as formatted.
            /// </summary>
            AssignVariableTypeFormatted,

            /// <summary>
            /// The CustomAction attributes have been renamed from BinaryKey and FileKey to BinaryRef and FileRef.
            /// </summary>
            CustomActionKeysAreNowRefs,

            /// <summary>
            /// The Product and Package elements have been renamed and reorganized.
            /// </summary>
            ProductAndPackageRenamed,

            /// <summary>
            /// The Module and Package elements have been renamed and reorganized.
            /// </summary>
            ModuleAndPackageRenamed,

            /// <summary>
            /// A MediaTemplate with no attributes set is now provided by default.
            /// </summary>
            DefaultMediaTemplate,

            /// <summary>
            /// util:RegistrySearch has breaking change when value is missing.
            /// </summary>
            UtilRegistryValueSearchBehaviorChange,

            /// <summary>
            /// DisplayInternalUI can't be converted.
            /// </summary>
            DisplayInternalUiNotConvertable,

            /// <summary>
            /// InstallerVersion has breaking change when missing.
            /// </summary>
            InstallerVersionBehaviorChange,

            /// <summary>
            /// Verb/@Target can't be converted.
            /// </summary>
            VerbTargetNotConvertable,

            /// <summary>
            /// The bootstrapper application dll is now specified in its own element.
            /// </summary>
            BootstrapperApplicationDll,

            /// <summary>
            /// The new bootstrapper application dll element is required.
            /// </summary>
            BootstrapperApplicationDllRequired,

            /// <summary>
            /// bal:UseUILanguages is deprecated, 'true' is now the standard behavior.
            /// </summary>
            BalUseUILanguagesDeprecated,

            /// <summary>
            /// The custom elements for built-in BAs are now required.
            /// </summary>
            BalBootstrapperApplicationRefToElement,
        }
    }
}

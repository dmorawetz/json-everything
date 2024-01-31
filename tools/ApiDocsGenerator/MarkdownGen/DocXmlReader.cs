using System.Reflection;
using System.Xml;
using System.Xml.XPath;

namespace ApiDocsGenerator.MarkdownGen;

/// <summary>
///     Helper class that reads XML documentation generated by C# compiler from code comments.
/// </summary>
public class DocXmlReader
{
	/// <summary>
	///     Dictionary of XML navigators for multiple assemblies.
	/// </summary>
	private readonly Dictionary<Assembly, XPathNavigator?> _assemblyNavigators;

	/// <summary>
	///     Function that returns path to XML documentation file for specified assembly.
	/// </summary>
	private readonly Func<Assembly, string?> _assemblyXmlPathFunction;

	/// <summary>
	///     Default value is true.
	///     When it is set to true DocXmlReader removes leading spaces and an empty
	///     lines at the end of the comment.
	///     By default XML comments are indented for human readability but it adds
	///     leading spaces that are not present in source code.
	///     For example here is compiler generated XML documentation with '-'
	///     showing spaces for readability.
	///     ----
	///     <summary>
	///         ----Text
	///         ----
	///     </summary>
	///     With UnIndentText set to true returned summary text is just "Text"
	///     With UnIndentText set to false returned summary text contains leading spaces
	///     and the trailing empty line "\n----Text\n----"
	/// </summary>
	private readonly bool _unIndentText;

	/// <summary>
	///     Open XML documentation files based on assemblies of types. Comment file names
	///     are generated based on assembly names by replacing assembly location with .xml.
	/// </summary>
	/// <param name="assemblyXmlPathFunction">
	///     Function that returns path to the assembly XML comment file.
	///     If function is null then comments file is assumed to have the same file name as assembly.
	///     If function returns null or if comments file does not exist then all comments for types from that
	///     assembly would remain empty.
	/// </param>
	public DocXmlReader(Func<Assembly, string?> assemblyXmlPathFunction)
	{
		_assemblyNavigators = [];
		_unIndentText = true;
		_assemblyXmlPathFunction = assemblyXmlPathFunction;
	}

	#region Public methods

	/// <summary>
	///     Returns comments for the class method. May return null object is comments for method
	///     are missing in XML documentation file.
	///     Returned comments tags:
	///     Summary, Remarks, Parameters (if present), Responses (if present), Returns
	/// </summary>
	/// <param name="methodInfo"></param>
	/// <param name="nullIfNoComment">Return null if comment for method is not available</param>
	/// <returns></returns>
	public async Task<MethodComments?> GetMethodComments(MethodBase methodInfo, bool nullIfNoComment)
	{
		var methodNode = await GetXmlMemberNode(methodInfo.MethodId(), methodInfo.ReflectedType);
		if (nullIfNoComment && methodNode == null) return null;
		var comments = new MethodComments();
		return GetComments(comments, methodNode);
	}

	private MethodComments GetComments(MethodComments comments, XPathNavigator? node)
	{
		if (node == null) return comments;

		GetCommonComments(comments, node);
		comments.Parameters = GetParametersComments(node);
		comments.TypeParameters = GetNamedComments(node, _typeParamXPath, _nameAttribute);
		comments.Returns = GetReturnsComment(node);
		comments.Responses = GetNamedComments(node, _responsesXPath, _codeAttribute);
		return comments;
	}

	/// <summary>
	///     Return Summary comments for specified type.
	///     For Delegate types Parameters field may be returned as well.
	/// </summary>
	/// <param name="type"></param>
	/// <returns>TypeComment</returns>
	public async Task<TypeComments> GetTypeComments(Type type)
	{
		var comments = new TypeComments();
		var node = await GetXmlMemberNode(type.TypeId(), type);
		return GetComments(type, comments, node);
	}

	private TypeComments GetComments(Type type, TypeComments comments, XPathNavigator? node)
	{
		if (node == null) return comments;
		if (type.IsSubclassOf(typeof(Delegate)))
		{
			comments.Parameters = GetParametersComments(node);
			comments.Returns = GetReturnsComment(node);
		}
		GetCommonComments(comments, node);
		return comments;
	}

	/// <summary>
	///     Returns comments for specified class member.
	/// </summary>
	/// <param name="memberInfo"></param>
	/// <returns></returns>
	public async Task<CommonComments> GetMemberComments(MemberInfo memberInfo)
	{
		var comments = new CommonComments();
		var node = await GetXmlMemberNode(memberInfo.MemberId(), memberInfo.ReflectedType);
		return GetComments(comments, node);
	}

	private CommonComments GetComments(CommonComments comments, XPathNavigator? node)
	{
		if (node == null) return comments;

		GetCommonComments(comments, node);
		return comments;
	}

	/// <summary>
	///     Get enum type description and comments for enum values. If <paramref name="fillValues" />
	///     is false and no comments exist for any value then ValueComments list is empty.
	/// </summary>
	/// <param name="enumType">
	///     Enum type to get comments for. If this is not an enum type then functions throws an
	///     ArgumentException
	/// </param>
	/// <param name="fillValues">
	///     True if ValueComments list should be filled even if
	///     none of the enum values have any summary comments
	/// </param>
	/// <returns>EnumComment</returns>
	public async Task<EnumComments> GetEnumComments(Type enumType, bool fillValues = false)
	{
		if (!enumType.IsEnum) throw new ArgumentException(nameof(enumType));

		var comments = new EnumComments();
		var typeNode = await GetXmlMemberNode(enumType.TypeId(), enumType);
		if (typeNode != null) GetCommonComments(comments, typeNode);

		var valueCommentsExist = false;
		foreach (var enumName in enumType.GetEnumNames())
		{
			var valueNode = await GetXmlMemberNode(enumType.EnumValueId(enumName), enumType);
			valueCommentsExist |= valueNode != null;
			var valueComment = new EnumValueComment(enumName, (int)Enum.Parse(enumType, enumName));
			comments.ValueComments.Add(valueComment);
			GetCommonComments(valueComment, valueNode);
		}

		if (!valueCommentsExist && !fillValues) comments.ValueComments.Clear();
		return comments;
	}

	#endregion

	#region XML items and atribute names

	// XPath strings and XML attribute names
	private const string _memberXPath = "/doc/members/member[@name='{0}']";
	private const string _summaryXPath = "summary";
	private const string _remarksXPath = "remarks";
	private const string _exampleXPath = "example";
	private const string _paramXPath = "param";
	private const string _typeParamXPath = "typeparam";
	private const string _responsesXPath = "response";
	private const string _returnsXPath = "returns";

	//  XML attribute names
	private const string _nameAttribute = "name";
	private const string _codeAttribute = "code";

	#endregion

	#region XML helper functions

	private void GetCommonComments(CommonComments comments, XPathNavigator? rootNode)
	{
		comments.Summary = GetSummaryComment(rootNode);
		comments.Remarks = GetRemarksComment(rootNode);
		comments.Example = GetExampleComment(rootNode);
	}

	private async Task<XPathNavigator?> GetXmlMemberNode(string name, Type? typeForAssembly, bool searchAllCurrentFiles = false)
	{
		var node = await GetXmlMemberNodeFromDictionary(name, typeForAssembly);
		if (node != null ||
			!searchAllCurrentFiles ||
			_assemblyNavigators.Count <= 1 && typeForAssembly != null) return node;
		foreach (var docNavigator in _assemblyNavigators.Values)
		{
			node = docNavigator?.SelectSingleNode(string.Format(_memberXPath, name));
			if (node != null) break;
		}

		return node;
	}

	private async Task<XPathNavigator?> GetXmlMemberNodeFromDictionary(string name, Type? typeForAssembly)
	{
		var typeNavigator = await GetNavigatorForAssembly(typeForAssembly?.Assembly);
		return typeNavigator?.SelectSingleNode(string.Format(_memberXPath, name));
	}

	private async Task<XPathNavigator?> GetNavigatorForAssembly(Assembly? assembly)
	{
		if (assembly == null) return null;
		if (_assemblyNavigators.TryGetValue(assembly, out var typeNavigator)) return typeNavigator;

		var commentFileName = _assemblyXmlPathFunction(assembly);
		if (commentFileName == null)
		{
			_assemblyNavigators.Add(assembly, null);
			return null;
		}

		var response = await File.ReadAllTextAsync(commentFileName);
		var document = new XmlDocument();
		document.LoadXml(response);
		var docNavigator = document.CreateNavigator();
		if (docNavigator == null) return null;

		_assemblyNavigators.Add(assembly, docNavigator);
		return docNavigator;
	}

	private string GetXmlText(XPathNavigator? node)
	{
		var innerText = node?.InnerXml ?? "";
		if (!_unIndentText || string.IsNullOrEmpty(innerText)) return innerText;

		var outerText = node?.OuterXml ?? "";
		var indentText = FindIndent(outerText);
		if (string.IsNullOrEmpty(indentText)) return innerText;
		return innerText.Replace(indentText, indentText[0].ToString()).Trim('\r', '\n');
	}

	private static string FindIndent(string? outerText)
	{
		if (string.IsNullOrEmpty(outerText)) return "";
		var end = outerText.LastIndexOf("</", StringComparison.Ordinal);
		if (end < 0) return "";
		var start = end - 1;
		// ReSharper disable once EmptyEmbeddedStatement
		for (; start >= 0 && outerText[start] != '\r' && outerText[start] != '\n'; start--) ;
		if (start < 0 || end <= start) return "";
		return outerText[start..end];
	}


	private string GetSummaryComment(XPathNavigator? rootNode)
	{
		return GetXmlText(rootNode?.SelectSingleNode(_summaryXPath));
	}

	private string GetRemarksComment(XPathNavigator? rootNode)
	{
		return GetXmlText(rootNode?.SelectSingleNode(_remarksXPath));
	}

	private string GetExampleComment(XPathNavigator? rootNode)
	{
		return GetXmlText(rootNode?.SelectSingleNode(_exampleXPath));
	}

	private string GetReturnsComment(XPathNavigator? methodNode)
	{
		var responseNodes = methodNode?.Select(_returnsXPath);
		if (responseNodes?.MoveNext() == true)
			return GetXmlText(responseNodes.Current);
		return "";
	}

	private List<(string Name, string Text)> GetParametersComments(XPathNavigator rootNode)
	{
		return GetNamedComments(rootNode, _paramXPath, _nameAttribute);
	}

	private List<(string Name, string Text)> GetNamedComments(XPathNavigator rootNode, string path, string attributeName)
	{
		var list = new List<(string Name, string Text)>();
		var childNodes = rootNode.Select(path);

		while (childNodes.MoveNext())
		{
			var code = childNodes.Current?.GetAttribute(attributeName, "");
			if (code == null) continue;
			list.Add((code, GetXmlText(childNodes.Current)));
		}

		return list;
	}

	#endregion
}

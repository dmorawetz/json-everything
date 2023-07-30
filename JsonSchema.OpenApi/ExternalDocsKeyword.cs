﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Json.Schema.OpenApi;

/// <summary>
/// Handles `example`.
/// </summary>
[SchemaKeyword(Name)]
[SchemaSpecVersion(SpecVersion.Draft202012)]
[Vocabulary(Vocabularies.OpenApiId)]
[JsonConverter(typeof(ExternalDocsKeywordJsonConverter))]
public class ExternalDocsKeyword : IJsonSchemaKeyword
{
	internal const string Name = "externalDocs";

	private JsonNode? _json;

	/// <summary>
	/// The URL for the target documentation. This MUST be in the form of a URL.
	/// </summary>
	public Uri Url { get; }
	/// <summary>
	/// A description of the target documentation. CommonMark syntax MAY be used for rich text representation.
	/// </summary>
	public string? Description { get; }
	/// <summary>
	/// Allows extensions to the OpenAPI Schema. The field name MUST begin with `x-`, for example,
	/// `x-internal-id`. Field names beginning `x-oai-` and `x-oas-` are reserved for uses defined by the OpenAPI Initiative.
	/// The value can be null, a primitive, an array or an object.
	/// </summary>
	public IReadOnlyDictionary<string, JsonNode?>? Extensions { get; }

	/// <summary>
	/// Creates a new <see cref="ExternalDocsKeyword"/>.
	/// </summary>
	/// <param name="url">The URL for the target documentation. This MUST be in the form of a URL.</param>
	/// <param name="description">A description of the target documentation. CommonMark syntax MAY be used for rich text representation.</param>
	/// <param name="extensions">
	/// Allows extensions to the OpenAPI Schema. The field name MUST begin with `x-`, for example,
	/// `x-internal-id`. Field names beginning `x-oai-` and `x-oas-` are reserved for uses defined by the OpenAPI Initiative.
	/// The value can be null, a primitive, an array or an object.
	/// </param>
	public ExternalDocsKeyword(Uri url, string? description, IReadOnlyDictionary<string, JsonNode?>? extensions)
	{
		Url = url;
		Description = description;
		Extensions = extensions;

		_json = JsonSerializer.SerializeToNode(this);
	}
	internal ExternalDocsKeyword(Uri url, string? description, IReadOnlyDictionary<string, JsonNode?>? extensions, JsonNode? json)
		: this(url, description, extensions)
	{
		_json = json;
	}

	public KeywordConstraint GetConstraint(SchemaConstraint schemaConstraint,
		IReadOnlyList<KeywordConstraint> localConstraints,
		EvaluationContext context)
	{
		return new KeywordConstraint(Name, (e, _) => e.Results.SetAnnotation(Name, _json));
	}
}

internal class ExternalDocsKeywordJsonConverter : JsonConverter<ExternalDocsKeyword>
{
	private class Model
	{
#pragma warning disable CS8618
		// ReSharper disable UnusedAutoPropertyAccessor.Local
		[JsonPropertyName("url")]
		public Uri Url { get; set; }
		[JsonPropertyName("description")]
		public string? Description { get; set; }
#pragma warning restore CS8618
		// ReSharper restore UnusedAutoPropertyAccessor.Local
	}

	public override ExternalDocsKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var node = JsonSerializer.Deserialize<JsonNode>(ref reader, options);

		var model = node.Deserialize<Model>(options);

		var extensionData = node!.AsObject().Where(x => x.Key.StartsWith("x-"))
			.ToDictionary(x => x.Key, x => x.Value);

		return new ExternalDocsKeyword(model!.Url, model.Description, extensionData, node);
	}
	public override void Write(Utf8JsonWriter writer, ExternalDocsKeyword value, JsonSerializerOptions options)
	{
		writer.WritePropertyName(DiscriminatorKeyword.Name);
		writer.WriteStartObject();
		writer.WriteString("propertyName", value.Url.OriginalString);
		if (value.Description != null)
			writer.WriteString("description", value.Description);

		if (value.Extensions != null)
		{
			foreach (var extension in value.Extensions)
			{
				writer.WritePropertyName(extension.Key);
				JsonSerializer.Serialize(writer, extension.Value, options);
			}
		}
	}
}
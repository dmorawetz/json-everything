﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Pointer;

namespace Json.Schema;

/// <summary>
/// Handles `anyOf`.
/// </summary>
[SchemaPriority(20)]
[SchemaKeyword(Name)]
[SchemaSpecVersion(SpecVersion.Draft6)]
[SchemaSpecVersion(SpecVersion.Draft7)]
[SchemaSpecVersion(SpecVersion.Draft201909)]
[SchemaSpecVersion(SpecVersion.Draft202012)]
[SchemaSpecVersion(SpecVersion.DraftNext)]
[Vocabulary(Vocabularies.Applicator201909Id)]
[Vocabulary(Vocabularies.Applicator202012Id)]
[Vocabulary(Vocabularies.ApplicatorNextId)]
[JsonConverter(typeof(AnyOfKeywordJsonConverter))]
public class AnyOfKeyword : IJsonSchemaKeyword, ISchemaCollector
{
	/// <summary>
	/// The JSON name of the keyword.
	/// </summary>
	public const string Name = "anyOf";

	/// <summary>
	/// The keywords schema collection.
	/// </summary>
	public IReadOnlyList<JsonSchema> Schemas { get; }

	/// <summary>
	/// Creates a new <see cref="AnyOfKeyword"/>.
	/// </summary>
	/// <param name="values">The set of schemas.</param>
	public AnyOfKeyword(params JsonSchema[] values)
	{
		Schemas = values.ToReadOnlyList() ?? throw new ArgumentNullException(nameof(values));
	}

	/// <summary>
	/// Creates a new <see cref="AnyOfKeyword"/>.
	/// </summary>
	/// <param name="values">The set of schemas.</param>
	public AnyOfKeyword(IEnumerable<JsonSchema> values)
	{
		Schemas = values.ToReadOnlyList() ?? throw new ArgumentNullException(nameof(values));
	}

	public KeywordConstraint GetConstraint(SchemaConstraint schemaConstraint, IReadOnlyList<KeywordConstraint> localConstraints, EvaluationContext context)
	{
		var subschemaConstraints = Schemas.Select((x, i) => x.GetConstraint(JsonPointer.Create(Name, i), schemaConstraint.BaseInstanceLocation, JsonPointer.Empty, context)).ToArray();

		return new KeywordConstraint(Name, Evaluator)
		{
			ChildDependencies = subschemaConstraints
		};
	}

	private static void Evaluator(KeywordEvaluation evaluation, EvaluationContext context)
	{
		if (evaluation.ChildEvaluations.All(x => !x.Results.IsValid))
			evaluation.Results.Fail();
	}
}

internal class AnyOfKeywordJsonConverter : JsonConverter<AnyOfKeyword>
{
	public override AnyOfKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.StartArray)
		{
			var schemas = JsonSerializer.Deserialize<List<JsonSchema>>(ref reader, options)!;
			return new AnyOfKeyword(schemas);
		}

		var schema = JsonSerializer.Deserialize<JsonSchema>(ref reader, options)!;
		return new AnyOfKeyword(schema);
	}
	public override void Write(Utf8JsonWriter writer, AnyOfKeyword value, JsonSerializerOptions options)
	{
		writer.WritePropertyName(AnyOfKeyword.Name);
		writer.WriteStartArray();
		foreach (var schema in value.Schemas)
		{
			JsonSerializer.Serialize(writer, schema, options);
		}
		writer.WriteEndArray();
	}
}
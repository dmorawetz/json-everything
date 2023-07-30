﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Pointer;

namespace Json.Schema;

/// <summary>
/// Handles `not`.
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
[JsonConverter(typeof(NotKeywordJsonConverter))]
public class NotKeyword : IJsonSchemaKeyword, ISchemaContainer
{
	/// <summary>
	/// The JSON name of the keyword.
	/// </summary>
	public const string Name = "not";

	/// <summary>
	/// The schema to not match.
	/// </summary>
	public JsonSchema Schema { get; }

	/// <summary>
	/// Creates a new <see cref="NotKeyword"/>.
	/// </summary>
	/// <param name="value">The schema to not match.</param>
	public NotKeyword(JsonSchema value)
	{
		Schema = value ?? throw new ArgumentNullException(nameof(value));
	}

	public KeywordConstraint GetConstraint(SchemaConstraint schemaConstraint, IReadOnlyList<KeywordConstraint> localConstraints, EvaluationContext context)
	{
		var subschemaConstraint = Schema.GetConstraint(JsonPointer.Create(Name), schemaConstraint.BaseInstanceLocation, JsonPointer.Empty, context);

		return new KeywordConstraint(Name, Evaluator)
		{
			ChildDependencies = new[] { subschemaConstraint }
		};
	}

	private static void Evaluator(KeywordEvaluation evaluation, EvaluationContext context)
	{
		if (evaluation.ChildEvaluations[0].Results.IsValid)
			evaluation.Results.Fail();
	}
}

internal class NotKeywordJsonConverter : JsonConverter<NotKeyword>
{
	public override NotKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var schema = JsonSerializer.Deserialize<JsonSchema>(ref reader, options)!;

		return new NotKeyword(schema);
	}
	public override void Write(Utf8JsonWriter writer, NotKeyword value, JsonSerializerOptions options)
	{
		writer.WritePropertyName(NotKeyword.Name);
		JsonSerializer.Serialize(writer, value.Schema, options);
	}
}
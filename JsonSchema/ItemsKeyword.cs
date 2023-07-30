﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Json.Pointer;

namespace Json.Schema;

/// <summary>
/// Handles `items`.
/// </summary>
[SchemaPriority(5)]
[SchemaKeyword(Name)]
[SchemaSpecVersion(SpecVersion.Draft6)]
[SchemaSpecVersion(SpecVersion.Draft7)]
[SchemaSpecVersion(SpecVersion.Draft201909)]
[SchemaSpecVersion(SpecVersion.Draft202012)]
[SchemaSpecVersion(SpecVersion.DraftNext)]
[Vocabulary(Vocabularies.Applicator201909Id)]
[Vocabulary(Vocabularies.Applicator202012Id)]
[Vocabulary(Vocabularies.ApplicatorNextId)]
[DependsOnAnnotationsFrom(typeof(PrefixItemsKeyword))]
[JsonConverter(typeof(ItemsKeywordJsonConverter))]
public class ItemsKeyword : IJsonSchemaKeyword, ISchemaContainer, ISchemaCollector
{
	/// <summary>
	/// The JSON name of the keyword.
	/// </summary>
	public const string Name = "items";

	/// <summary>
	/// The schema for the "single schema" form.
	/// </summary>
	public JsonSchema? SingleSchema { get; }

	JsonSchema ISchemaContainer.Schema => SingleSchema!;

	/// <summary>
	/// The collection of schemas for the "schema array" form.
	/// </summary>
	public IReadOnlyList<JsonSchema>? ArraySchemas { get; }

	IReadOnlyList<JsonSchema> ISchemaCollector.Schemas => ArraySchemas!;

	/// <summary>
	/// Creates a new <see cref="ItemsKeyword"/>.
	/// </summary>
	/// <param name="value">The schema for the "single schema" form.</param>
	public ItemsKeyword(JsonSchema value)
	{
		SingleSchema = value ?? throw new ArgumentNullException(nameof(value));
	}

	/// <summary>
	/// Creates a new <see cref="ItemsKeyword"/>.
	/// </summary>
	/// <param name="values">The collection of schemas for the "schema array" form.</param>
	/// <remarks>
	/// Using the `params` constructor to build an array-form `items` keyword with a single schema
	/// will confuse the compiler.  To achieve this, you'll need to explicitly specify the array.
	/// </remarks>
	public ItemsKeyword(params JsonSchema[] values)
	{
		ArraySchemas = values.ToReadOnlyList();
	}

	/// <summary>
	/// Creates a new <see cref="ItemsKeyword"/>.
	/// </summary>
	/// <param name="values">The collection of schemas for the "schema array" form.</param>
	public ItemsKeyword(IEnumerable<JsonSchema> values)
	{
		ArraySchemas = values.ToReadOnlyList();
	}

	public KeywordConstraint GetConstraint(SchemaConstraint schemaConstraint,
		IReadOnlyList<KeywordConstraint> localConstraints,
		EvaluationContext context)
	{
		var constraint = new KeywordConstraint(Name, Evaluator);

		if (SingleSchema != null)
		{
			var prefixItemsConstraint = localConstraints.FirstOrDefault(x => x.Keyword == PrefixItemsKeyword.Name);

			var subschemaConstraint = SingleSchema.GetConstraint(JsonPointer.Create(Name), schemaConstraint.BaseInstanceLocation, JsonPointer.Empty, context);
			subschemaConstraint.InstanceLocator = evaluation =>
			{
				if (evaluation.LocalInstance is not JsonArray array) return Array.Empty<JsonPointer>();

				if (array.Count == 0) return Array.Empty<JsonPointer>();

				var startIndex = 0;

				var prefixItemsEvaluation = evaluation.GetKeywordEvaluation<PrefixItemsKeyword>();
				if (prefixItemsEvaluation != null)
					startIndex = prefixItemsEvaluation.ChildEvaluations.Length;

				if (array.Count <= startIndex) return Array.Empty<JsonPointer>();

				return Enumerable.Range(startIndex, array.Count - startIndex).Select(x => JsonPointer.Create(x));
			};

			if (prefixItemsConstraint != null)
				constraint.SiblingDependencies = new[] { prefixItemsConstraint };
			constraint.ChildDependencies = new[] { subschemaConstraint };
		}
		else // ArraySchema
		{
			if (context.Options.EvaluatingAs.HasFlag(SpecVersion.Draft202012) ||
			    context.Options.EvaluatingAs.HasFlag(SpecVersion.DraftNext))
				throw new JsonSchemaException($"Array form of {Name} is invalid for draft 2020-12 and later");

			var subschemaConstraints = ArraySchemas!.Select((x, i) => x.GetConstraint(JsonPointer.Create(Name, i), schemaConstraint.BaseInstanceLocation, JsonPointer.Create(i), context)).ToArray();

			constraint.ChildDependencies = subschemaConstraints;
		}

		return constraint;
	}

	private static void Evaluator(KeywordEvaluation evaluation, EvaluationContext context)
	{
		if (evaluation.LocalInstance is not JsonArray array || array.Count == 0) return;

		var lastItem = array.Last();
		// can't check by count because items may not have evaluated all of the items
		// check that the last item was evaluated instead
		if (evaluation.ChildEvaluations.Any(x => ReferenceEquals(x.LocalInstance, lastItem)))
			evaluation.Results.SetAnnotation(Name, true);
		else
			evaluation.Results.SetAnnotation(Name, evaluation.ChildEvaluations.Length - 1);
	
		if (!evaluation.ChildEvaluations.All(x => x.Results.IsValid))
			evaluation.Results.Fail();
	}
}

internal class ItemsKeywordJsonConverter : JsonConverter<ItemsKeyword>
{
	public override ItemsKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.StartArray)
		{
			var schemas = JsonSerializer.Deserialize<List<JsonSchema>>(ref reader, options)!;
			return new ItemsKeyword(schemas);
		}

		var schema = JsonSerializer.Deserialize<JsonSchema>(ref reader, options)!;
		return new ItemsKeyword(schema);
	}
	public override void Write(Utf8JsonWriter writer, ItemsKeyword value, JsonSerializerOptions options)
	{
		writer.WritePropertyName(ItemsKeyword.Name);
		if (value.SingleSchema != null)
			JsonSerializer.Serialize(writer, value.SingleSchema, options);
		else
		{
			writer.WriteStartArray();
			foreach (var schema in value.ArraySchemas!)
			{
				JsonSerializer.Serialize(writer, schema, options);
			}
			writer.WriteEndArray();
		}
	}
}

public static partial class ErrorMessages
{
	private static string? _invalidItemsForm;

	/// <summary>
	/// Gets or sets the error message for when <see cref="ItemsKeyword"/> is specified
	/// with an array of schemas in a draft 2020-12 or later schema.
	/// </summary>
	/// <remarks>No tokens are supported.</remarks>
	public static string InvalidItemsForm
	{
		get => _invalidItemsForm ?? Get();
		set => _invalidItemsForm = value;
	}
}
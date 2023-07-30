﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Json.More;
using Json.Pointer;

namespace Json.Schema;

/// <summary>
/// Handles `unevaluatedItems`.
/// </summary>
[SchemaPriority(30)]
[SchemaKeyword(Name)]
[SchemaSpecVersion(SpecVersion.Draft201909)]
[SchemaSpecVersion(SpecVersion.Draft202012)]
[SchemaSpecVersion(SpecVersion.DraftNext)]
[Vocabulary(Vocabularies.Applicator201909Id)]
[Vocabulary(Vocabularies.Applicator202012Id)]
[Vocabulary(Vocabularies.ApplicatorNextId)]
[DependsOnAnnotationsFrom(typeof(PrefixItemsKeyword))]
[DependsOnAnnotationsFrom(typeof(ItemsKeyword))]
[DependsOnAnnotationsFrom(typeof(AdditionalItemsKeyword))]
[DependsOnAnnotationsFrom(typeof(ContainsKeyword))]
[DependsOnAnnotationsFrom(typeof(UnevaluatedItemsKeyword))]
[JsonConverter(typeof(UnevaluatedItemsKeywordJsonConverter))]
public class UnevaluatedItemsKeyword : IJsonSchemaKeyword, ISchemaContainer
{
	/// <summary>
	/// The JSON name of the keyword.
	/// </summary>
	public const string Name = "unevaluatedItems";

	/// <summary>
	/// The schema by which to evaluate unevaluated items.
	/// </summary>
	public JsonSchema Schema { get; }

	/// <summary>
	/// Creates a new <see cref="UnevaluatedItemsKeyword"/>.
	/// </summary>
	/// <param name="value">The schema by which to evaluate unevaluated items.</param>
	public UnevaluatedItemsKeyword(JsonSchema value)
	{
		Schema = value ?? throw new ArgumentNullException(nameof(value));
	}

	public KeywordConstraint GetConstraint(SchemaConstraint schemaConstraint, IReadOnlyList<KeywordConstraint> localConstraints, EvaluationContext context)
	{
		return new KeywordConstraint(Name, Evaluator);
	}

	private void Evaluator(KeywordEvaluation evaluation, EvaluationContext context)
	{
		static bool CheckAnnotation<T>(EvaluationResults results)
			where T : IJsonSchemaKeyword
		{
			var annotations = results.GetAllAnnotations(typeof(T).Keyword());
			return annotations.Any(x => x.IsEquivalentTo(true));
		}

		if (evaluation.LocalInstance is not JsonArray array)
		{
			evaluation.MarkAsSkipped();
			return;
		}

		if (CheckAnnotation<AdditionalItemsKeyword>(evaluation.Results) ||
		    CheckAnnotation<UnevaluatedItemsKeyword>(evaluation.Results))
		{
			evaluation.MarkAsSkipped();
			return;
		}

		var startIndex = 0;
		var itemsAnnotations = evaluation.Results.GetAllAnnotations(ItemsKeyword.Name);
		foreach (var itemsAnnotation in itemsAnnotations)
		{
			if (itemsAnnotation.IsEquivalentTo(true))
			{
				evaluation.MarkAsSkipped();
				return;
			}

			startIndex = Math.Max(startIndex, itemsAnnotation!.GetValue<int>() + 1);
		}

		var prefixItemsAnnotations = evaluation.Results.GetAllAnnotations(PrefixItemsKeyword.Name);
		foreach (var prefixItemsAnnotation in prefixItemsAnnotations)
		{
			if (prefixItemsAnnotation.IsEquivalentTo(true))
			{
				evaluation.MarkAsSkipped();
				return;
			}

			startIndex = Math.Max(startIndex, prefixItemsAnnotation!.GetValue<int>() + 1);
		}

		var indicesToEvaluate = Enumerable.Range(startIndex, array.Count - startIndex);
		if (context.EvaluatingAs is SpecVersion.Draft202012 or SpecVersion.DraftNext or SpecVersion.Unspecified)
		{
			var containsAnnotations = evaluation.Results.GetAllAnnotations(ContainsKeyword.Name);
			indicesToEvaluate = indicesToEvaluate.Except(containsAnnotations.SelectMany(x => x!.AsArray()).Select(x => x!.GetValue<int>()));
		}

		var childEvaluations = indicesToEvaluate
			.Select(i => (Index: i, Constraint: Schema.GetConstraint(JsonPointer.Create(Name), evaluation.Results.InstanceLocation, JsonPointer.Create(i), context)))
			.Select(x => x.Constraint.BuildEvaluation(array[x.Index], evaluation.Results.InstanceLocation.Combine(x.Index), evaluation.Results.EvaluationPath, context.Options))
			.ToArray();

		evaluation.ChildEvaluations = childEvaluations;
		foreach (var childEvaluation in childEvaluations)
		{
			childEvaluation.Evaluate(context);
		}

		evaluation.Results.SetAnnotation(Name, true);

		if (!evaluation.ChildEvaluations.All(x => x.Results.IsValid))
			evaluation.Results.Fail();
	}
}

internal class UnevaluatedItemsKeywordJsonConverter : JsonConverter<UnevaluatedItemsKeyword>
{
	public override UnevaluatedItemsKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var schema = JsonSerializer.Deserialize<JsonSchema>(ref reader, options)!;

		return new UnevaluatedItemsKeyword(schema);
	}
	public override void Write(Utf8JsonWriter writer, UnevaluatedItemsKeyword value, JsonSerializerOptions options)
	{
		writer.WritePropertyName(UnevaluatedItemsKeyword.Name);
		JsonSerializer.Serialize(writer, value.Schema, options);
	}
}
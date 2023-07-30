﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.More;
using Json.Pointer;

namespace Json.Schema;

/// <summary>
/// Handles `type`.
/// </summary>
[SchemaKeyword(Name)]
[SchemaSpecVersion(SpecVersion.Draft6)]
[SchemaSpecVersion(SpecVersion.Draft7)]
[SchemaSpecVersion(SpecVersion.Draft201909)]
[SchemaSpecVersion(SpecVersion.Draft202012)]
[SchemaSpecVersion(SpecVersion.DraftNext)]
[Vocabulary(Vocabularies.Validation201909Id)]
[Vocabulary(Vocabularies.Validation202012Id)]
[Vocabulary(Vocabularies.ValidationNextId)]
[JsonConverter(typeof(TypeKeywordJsonConverter))]
public class TypeKeyword : IJsonSchemaKeyword
{
	/// <summary>
	/// The JSON name of the keyword.
	/// </summary>
	public const string Name = "type";

	/// <summary>
	/// The expected type.
	/// </summary>
	public SchemaValueType Type { get; }

	/// <summary>
	/// Creates a new <see cref="TypeKeyword"/>.
	/// </summary>
	/// <param name="type">The expected type.</param>
	public TypeKeyword(SchemaValueType type)
	{
		Type = type;
	}

	/// <summary>
	/// Creates a new <see cref="TypeKeyword"/>.
	/// </summary>
	/// <param name="types">The expected types.</param>
	public TypeKeyword(params SchemaValueType[] types)
	{
		// TODO: protect input

		Type = types.Aggregate((x, y) => x | y);
	}

	/// <summary>
	/// Creates a new <see cref="TypeKeyword"/>.
	/// </summary>
	/// <param name="types">The expected types.</param>
	public TypeKeyword(IEnumerable<SchemaValueType> types)
	{
		// TODO: protect input

		Type = types.Aggregate((x, y) => x | y);
	}

	public KeywordConstraint GetConstraint(SchemaConstraint schemaConstraint, IReadOnlyList<KeywordConstraint> localConstraints, EvaluationContext context)
	{
		return new KeywordConstraint(Name, (e, _) => Evaluator(e, Type));
	}

	private void Evaluator(KeywordEvaluation evaluation, SchemaValueType expectedType)
	{
		var instanceType = evaluation.LocalInstance.GetSchemaValueType();
		if (expectedType.HasFlag(instanceType)) return;
		if (instanceType == SchemaValueType.Integer && expectedType.HasFlag(SchemaValueType.Number)) return;
		if (instanceType == SchemaValueType.Number)
		{
			var number = evaluation.LocalInstance!.AsValue().GetNumber();
			if (number == Math.Truncate(number!.Value) && expectedType.HasFlag(SchemaValueType.Integer)) return;
		}

		var expected = expectedType.ToString().ToLower();
		evaluation.Results.Fail(Name, ErrorMessages.Type, ("received", instanceType), ("expected", expected));
	}
}

internal class TypeKeywordJsonConverter : JsonConverter<TypeKeyword>
{
	public override TypeKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var type = JsonSerializer.Deserialize<SchemaValueType>(ref reader, options);

		return new TypeKeyword(type);
	}
	public override void Write(Utf8JsonWriter writer, TypeKeyword value, JsonSerializerOptions options)
	{
		writer.WritePropertyName(TypeKeyword.Name);
		JsonSerializer.Serialize(writer, value.Type, options);
	}
}

public static partial class ErrorMessages
{
	private static string? _type;

	/// <summary>
	/// Gets or sets the error message for <see cref="TypeKeyword"/>.
	/// </summary>
	/// <remarks>
	///	Available tokens are:
	///   - [[received]] - the type of value provided in the JSON instance
	///   - [[expected]] - the type(s) required by the schema
	/// </remarks>
	public static string Type
	{
		get => _type ?? Get();
		set => _type = value;
	}
}
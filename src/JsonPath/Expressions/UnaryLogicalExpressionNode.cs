﻿using System.Diagnostics.CodeAnalysis;
using System;
using System.Text;
using System.Text.Json.Nodes;

namespace Json.Path.Expressions;

internal class UnaryLogicalExpressionNode : LogicalExpressionNode
{
	public IUnaryLogicalOperator Operator { get; }
	public BooleanResultExpressionNode Value { get; }

	public UnaryLogicalExpressionNode(IUnaryLogicalOperator op, BooleanResultExpressionNode value)
	{
		Operator = op;
		Value = value;
	}

	public override bool Evaluate(JsonNode? globalParameter, JsonNode? localParameter)
	{
		return Operator.Evaluate(Value.Evaluate(globalParameter, localParameter));
	}

	public override void BuildString(StringBuilder builder)
	{
		var useGroup = Value is BinaryComparativeExpressionNode or BinaryLogicalExpressionNode;

		builder.Append(Operator);
		if (useGroup)
			builder.Append('(');
		Value.BuildString(builder);
		if (useGroup)
			builder.Append(')');
	}

	public override string ToString()
	{
		return $"{Operator}{Value}";
	}
}

internal class UnaryLogicalExpressionParser : ILogicalExpressionParser
{
	public bool TryParse(ReadOnlySpan<char> source, ref int index, int nestLevel, [NotNullWhen(true)] out LogicalExpressionNode? expression, PathParsingOptions options)
	{
		// currently only the "not" operator is known
		// it expects a ! then either a comparison or logical expression

		var i = index;
		var originalNest = nestLevel; // need to get back to this

		if (!source.ConsumeWhitespace(ref index))
		{
			expression = null;
			return false;
		}

		while (i < source.Length && source[i] == '(')
		{
			nestLevel++;
			i++;
		}
		if (i == source.Length)
			throw new PathParseException(i, "Unexpected end of input");

		// parse operator
		if (!UnaryLogicalOperatorParser.TryParse(source, ref i, out var op))
		{
			expression = null;
			return false;
		}

		// parse comparison
		if (!BooleanResultExpressionParser.TryParse(source, ref i, nestLevel, out var right, options))
		{
			expression = null;
			return false;
		}

		if (!source.ConsumeWhitespace(ref index))
		{
			expression = null;
			return false;
		}

		while (i < source.Length && source[i] == ')' && nestLevel > originalNest)
		{
			nestLevel--;
			i++;
		}
		if (i == source.Length)
			throw new PathParseException(i, "Unexpected end of input");
		if (nestLevel != originalNest)
		{
			expression = null;
			return false;
		}

		expression = new UnaryLogicalExpressionNode(op, right);
		index = i;
		return true;
	}
}
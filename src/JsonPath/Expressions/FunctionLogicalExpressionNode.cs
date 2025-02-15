﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace Json.Path.Expressions;

internal class FunctionLogicalExpressionNode : LogicalExpressionNode
{
	public IPathFunctionDefinition Function { get; }
	public ExpressionNode[] Parameters { get; }

	public FunctionLogicalExpressionNode(IPathFunctionDefinition function, IEnumerable<ExpressionNode> parameters)
	{
		Function = function;
		Parameters = parameters.ToArray();
	}

	public override bool Evaluate(JsonNode? globalParameter, JsonNode? localParameter)
	{
		var parameterValues = Parameters.Select(x =>
		{
			return x switch
			{
				ValueExpressionNode c => (object?)c.Evaluate(globalParameter, localParameter),
				LogicalExpressionNode b => b.Evaluate(globalParameter, localParameter),
				_ => throw new ArgumentOutOfRangeException("parameter")
			};
		}).ToArray();

		return Function switch
		{
			NodelistFunctionDefinition nFunc => nFunc.Invoke(parameterValues)?.Count != 0,
			LogicalFunctionDefinition lFunc => lFunc.Invoke(parameterValues) == true,
			_ => throw new ArgumentException("This shouldn't happen.  Logical functions are not valid here.")
		};
	}

	public override void BuildString(StringBuilder builder)
	{
		builder.Append(Function.Name);
		builder.Append('(');

		if (Parameters.Any())
		{
			Parameters[0].BuildString(builder);
			for (int i = 1; i < Parameters.Length; i++)
			{
				builder.Append(',');
				Parameters[i].BuildString(builder);
			}
		}

		builder.Append(')');
	}

	public override string ToString()
	{
		return $"{Function.Name}({string.Join(",", (IEnumerable<ValueExpressionNode>)Parameters)})";
	}
}

internal class FunctionLogicalExpressionParser : ILogicalExpressionParser
{
	public bool TryParse(ReadOnlySpan<char> source, ref int index, int nestLevel, [NotNullWhen(true)] out LogicalExpressionNode? expression, PathParsingOptions options)
	{
		int i = index;
		if (!FunctionExpressionParser.TryParseFunction(source, ref i, out var parameters, out var function, options))
		{
			expression = null;
			return false;
		}

		if (function is ValueFunctionDefinition)
		{
			expression = null;
			return false;
		}

		expression = new FunctionLogicalExpressionNode(function, parameters);
		index = i;
		return true;
	}
}

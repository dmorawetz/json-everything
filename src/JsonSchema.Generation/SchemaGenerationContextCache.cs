﻿using System;
using System.Collections.Generic;
using Json.Schema.Generation.Intents;

namespace Json.Schema.Generation;

/// <summary>
/// Gets the contexts for the current run.
/// </summary>
public static class SchemaGenerationContextCache
{
	[ThreadStatic]
	private static Dictionary<Type, SchemaGenerationContextBase>? _cache;

	internal static Dictionary<Type, SchemaGenerationContextBase> Cache => _cache ??= [];

	/// <summary>
	/// Gets or creates a <see cref="SchemaGenerationContextBase"/> based on the given
	/// type and attribute set.
	/// </summary>
	/// <param name="type">The type to generate.</param>
	/// <returns>
	/// A generation context, from the cache if one exists with the specified
	/// type and attribute set; otherwise a new one.  New contexts are automatically
	/// cached.
	/// </returns>
	/// <remarks>
	/// Use this in your generator if it needs to create keywords with subschemas.
	/// </remarks>
	public static SchemaGenerationContextBase Get(Type type)
	{
		return Get(type, false);
	}

	internal static SchemaGenerationContextBase GetRoot(Type type)
	{
		return Get(type, true);
	}

	private static SchemaGenerationContextBase Get(Type type, bool isRoot)
	{
		if (!Cache.TryGetValue(type, out var context))
		{
			context = new TypeGenerationContext(type) { IsRoot = isRoot };
			var comments = SchemaGeneratorConfiguration.Current.XmlReader.GetTypeComments(type);
			if (!string.IsNullOrWhiteSpace(comments.Summary))
				context.Intents.Add(new DescriptionIntent(comments.Summary!));

			Cache[type] = context;

			context.GenerateIntents();
		}

		context.ReferenceCount++;

		return context;
	}

	internal static void Clear()
	{
		Cache.Clear();
	}
}
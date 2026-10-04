using System;

namespace GoldSrc;

[AttributeUsage( AttributeTargets.Class, AllowMultiple = true, Inherited = false )]
public sealed class LinkEntityToClassAttribute( string className ) : Attribute
{
	public string ClassName { get; } = className;

	public static TypeDescription Find( string className )
	{
		foreach ( var (type, attribute) in Game.TypeLibrary.GetTypesWithAttribute<LinkEntityToClassAttribute>( false ) )
		{
			if ( attribute.ClassName == className )
				return type;
		}

		return null;
	}
}

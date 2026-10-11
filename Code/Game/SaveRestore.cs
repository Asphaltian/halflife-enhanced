using System;
using GoldSrc;

namespace HalfLife;

[AttributeUsage( AttributeTargets.Class )]
public sealed class SaveRestoreBaseAttribute : Attribute
{
	public Type BaseClass { get; }

	public SaveRestoreBaseAttribute( Type baseClass )
	{
		BaseClass = baseClass;
	}
}

public abstract class SaveRestoreBuffer
{
	protected SaveRestoreData Data { get; }
	protected List<SaveBlock> Header { get; }

	protected SaveRestoreBuffer( SaveRestoreData data, List<SaveBlock> header )
	{
		Data = data;
		Header = header;
	}

	[SkipHotload]
	private static readonly Dictionary<Type, (MemberDescription Member, SaveAttribute Save)[]> FieldCache = [];

	public int EntityIndex( Edict entity )
	{
		if ( Data is null || entity is null )
			return -1;

		return Array.FindIndex( Data.Table, x => x.Edict == entity );
	}

	public Edict EntityFromIndex( int entityIndex )
	{
		if ( Data is null || entityIndex < 0 )
			return null;

		return Array.Find( Data.Table, x => x.Id == entityIndex )?.Edict;
	}

	public int EntityFlagsSet( int entityIndex, int flags )
	{
		if ( Data is null || entityIndex < 0 || entityIndex > Data.Table.Length )
			return 0;

		Data.Table[entityIndex].Flags |= flags;

		return Data.Table[entityIndex].Flags;
	}

	protected static (MemberDescription Member, SaveAttribute Save)[] FieldsOf( Type type )
	{
		if ( FieldCache.TryGetValue( type, out var fields ) )
			return fields;

		var list = new List<(MemberDescription, SaveAttribute)>();

		foreach ( var member in Game.TypeLibrary.GetType( type ).DeclaredMembers )
		{
			if ( member.GetCustomAttribute<SaveAttribute>() is { } save )
				list.Add( (member, save) );
		}

		return FieldCache[type] = [.. list];
	}

	protected static object GetValue( MemberDescription member, object target )
	{
		return member is PropertyDescription property ? property.GetValue( target ) : ((FieldDescription)member).GetValue( target );
	}

	protected static void SetValue( MemberDescription member, object target, object value )
	{
		if ( member is PropertyDescription property )
			property.SetValue( target, value );
		else
			((FieldDescription)member).SetValue( target, value );
	}

	protected static Type TypeOf( MemberDescription member )
	{
		return member is PropertyDescription property ? property.PropertyType : ((FieldDescription)member).FieldType;
	}
}

public sealed class Save( SaveRestoreData data, List<SaveBlock> header = null ) : SaveRestoreBuffer( data, header )
{
	public bool WriteFields( string name, object source, Type type )
	{
		var block = new SaveBlock( name );

		foreach ( var (member, save) in FieldsOf( type ) )
		{
			var value = GetValue( member, source );

			if ( DataEmpty( value ) )
				continue;

			block.Fields[member.Name] = value is Array array ? WriteArray( array, save.Type, source ) : WriteValue( value, save.Type, source );
		}

		if ( Header is not null )
		{
			Header.Add( block );
			return true;
		}

		if ( Data is null )
			return true;

		Data.Current.Blocks ??= [];
		Data.Current.Blocks.Add( block );

		return true;
	}

	private object[] WriteArray( Array array, FieldType type, object source )
	{
		var values = new object[array.Length];

		for ( var i = 0; i < values.Length; i++ )
			values[i] = WriteValue( array.GetValue( i ), type, source );

		return values;
	}

	private object WriteValue( object value, FieldType type, object source )
	{
		switch ( type )
		{
			case FieldType.Time:
				return Data is null ? (float)value : (float)value - Data.Time;

			case FieldType.PositionVector:
				return Data is { UseLandmark: true } ? (Vector3)value - Data.LandmarkOffset : (Vector3)value;

			case FieldType.ClassPtr:
			case FieldType.EHandle:
			case FieldType.EntVars:
			case FieldType.Edict:
			case FieldType.Entity:
				return EntityIndex( value as Edict );

			case FieldType.Function:
				return NameForFunction( (Delegate)value, source );

			default:
				return value is Enum ? Convert.ToInt64( value ) : value;
		}
	}

	private static string NameForFunction( Delegate function, object source )
	{
		var parameterCount = function switch
		{
			Action => 0,
			Action<BaseEntity> => 1,
			BaseEntity.UseFunction => 4,
			_ => -1
		};

		for ( var type = Game.TypeLibrary.GetType( source.GetType() ); type is not null; type = type.BaseType )
		{
			foreach ( var member in type.DeclaredMembers )
			{
				if ( member is not MethodDescription method || method.IsStatic || method.Parameters.Length != parameterCount || method.ReturnType != typeof( void ) )
					continue;

				try
				{
					if ( function.Equals( method.CreateDelegate( function.GetType(), source ) ) )
						return method.Name;
				}
				catch ( ArgumentException )
				{
				}
			}
		}

		Util.Alert( AlertType.Error, "Invalid function pointer in entity!" );
		return null;
	}

	private static bool DataEmpty( object value )
	{
		return value switch
		{
			null => true,
			string text => text.Length == 0,
			float number => BitConverter.SingleToInt32Bits( number ) == 0,
			int number => number == 0,
			short number => number == 0,
			byte number => number == 0,
			bool flag => !flag,
			Vector3 vector => DataEmpty( vector.x ) && DataEmpty( vector.y ) && DataEmpty( vector.z ),
			Enum number => Convert.ToInt64( number ) == 0,
			Array array => IsEmptyArray( array ),
			_ => false
		};
	}

	private static bool IsEmptyArray( Array array )
	{
		foreach ( var element in array )
		{
			if ( !DataEmpty( element ) )
				return false;
		}

		return true;
	}
}

public sealed class Restore( SaveRestoreData data, List<SaveBlock> header = null ) : SaveRestoreBuffer( data, header )
{
	public bool Global { get; set; }

	public bool Precache { get; set; } = true;

	private int _position;

	public bool ReadFields( string name, object target, Type type )
	{
		var blocks = Header ?? Data.Current.Blocks;

		if ( blocks is null || _position >= blocks.Count || blocks[_position].Name != name )
			return false;

		var block = blocks[_position++];

		foreach ( var (member, save) in FieldsOf( type ) )
		{
			if ( Global && save.Global )
				continue;

			var current = GetValue( member, target );

			if ( current is Array array )
			{
				Array.Clear( array );

				if ( block.Fields.TryGetValue( member.Name, out var saved ) )
				{
					var values = (object[])saved;

					for ( var i = 0; i < values.Length && i < array.Length; i++ )
						array.SetValue( ReadValue( values[i], save.Type, target, array.GetValue( i )?.GetType() ), i );
				}

				continue;
			}

			SetValue( member, target, block.Fields.TryGetValue( member.Name, out var value )
				? ReadValue( value, save.Type, target, TypeOf( member ) )
				: Zero( current ) );
		}

		return true;
	}

	private object ReadValue( object value, FieldType type, object target, Type memberType )
	{
		switch ( type )
		{
			case FieldType.Time:
				return (float)value + Data.Time;

			case FieldType.PositionVector:
				return Data.UseLandmark ? (Vector3)value + Data.LandmarkOffset : (Vector3)value;

			case FieldType.ClassPtr:
			case FieldType.EHandle:
			case FieldType.EntVars:
			case FieldType.Edict:
			case FieldType.Entity:
				return EntityFromIndex( (int)value );

			case FieldType.Function:
				return FunctionFromName( (string)value, target, memberType );

			case FieldType.ModelName:
				{
					var model = Server.AllocString( (string)value );

					if ( Precache && model is { Length: > 0 } && target is Edict modelOwner )
						modelOwner.Server.PrecacheModel( model );

					return model;
				}

			case FieldType.SoundName:
				{
					var sound = Server.AllocString( (string)value );

					if ( Precache && sound is { Length: > 0 } && target is Edict soundOwner )
						soundOwner.Server.PrecacheSound( sound );

					return sound;
				}

			case FieldType.String:
				return Server.AllocString( (string)value );

			default:
				return memberType is { IsEnum: true } ? Enum.ToObject( memberType, value ) : value;
		}
	}

	private static Delegate FunctionFromName( string name, object target, Type delegateType )
	{
		if ( string.IsNullOrEmpty( name ) )
			return null;

		for ( var type = Game.TypeLibrary.GetType( target.GetType() ); type is not null; type = type.BaseType )
		{
			foreach ( var member in type.DeclaredMembers )
			{
				if ( member is MethodDescription method && !method.IsStatic && method.Name == name )
					return method.CreateDelegate( delegateType, target );
			}
		}

		return null;
	}

	private static object Zero( object value )
	{
		return value switch
		{
			float => 0f,
			int => 0,
			short => (short)0,
			byte => (byte)0,
			bool => false,
			Vector3 => default( Vector3 ),
			Enum number => Enum.ToObject( number.GetType(), 0 ),
			_ => null
		};
	}
}

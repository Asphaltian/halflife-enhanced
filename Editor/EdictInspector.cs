using System;
using System.Reflection;
using GoldSrc;
using HalfLife;

[CustomEditor( typeof( Edict ) )]
public sealed class EdictInspector : ComponentEditorWidget
{
	private const string SpawnFlagPrefix = "SpawnFlag";

	private static readonly MethodInfo CreateMethod = typeof( EdictInspector ).GetMethod( nameof( Create ), BindingFlags.NonPublic | BindingFlags.Static );
	private static readonly MethodInfo CreateElementMethod = typeof( EdictInspector ).GetMethod( nameof( CreateElement ), BindingFlags.NonPublic | BindingFlags.Static );
	private static readonly Attribute[] ReadOnlyAttributes = [new ReadOnlyAttribute()];

	public EdictInspector( SerializedObject serialized ) : base( serialized )
	{
		Layout = Layout.Column();
		Layout.Margin = new Sandbox.UI.Margin( 0, 4, 12, 4 );

		Edict[] targets = [.. serialized.Targets.OfType<Edict>()];

		if ( targets.Length == 0 )
			return;

		var sheet = new ControlSheet { IncludePropertyNames = true };

		Layout.Add( sheet );

		if ( targets[0] is BaseEntity )
		{
			var row = Layout.Row();

			row.Spacing = 4;
			row.Margin = new Sandbox.UI.Margin( 16, 4, 0, 4 );

			foreach ( var useType in Enum.GetValues<UseType>() )
			{
				if ( useType != UseType.Set )
					row.Add( new Button( $"Use {useType}" ) { Clicked = new Use( targets, useType ).Fire } );
			}

			row.AddStretchCell();
			sheet.AddLayout( row );
		}

		if ( SpawnFlagProperties( targets ) is { Length: > 0 } flags )
			sheet.AddGroup( "Spawn Flags", flags );

		for ( var type = EditorTypeLibrary.GetType( targets[0].GetType() ); type is not null && type.TargetType != typeof( Component ); type = type.BaseType )
		{
			var properties = new List<SerializedProperty>();

			foreach ( var member in type.DeclaredMembers )
			{
				if ( member.GetCustomAttribute<SaveAttribute>() is not null )
					AddProperties( properties, member, targets );
			}

			if ( properties.Count > 0 )
				sheet.AddGroup( type.Title, [.. properties] );
		}
	}

	private static SerializedProperty[] SpawnFlagProperties( Edict[] targets )
	{
		var properties = new SortedList<uint, SerializedProperty>();

		for ( var type = targets[0].GetType(); type is not null && type != typeof( Component ); type = type.BaseType )
		{
			foreach ( var field in type.GetFields( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly ) )
			{
				if ( !field.IsLiteral || field.FieldType != typeof( int ) || !field.Name.StartsWith( SpawnFlagPrefix, StringComparison.Ordinal ) )
					continue;

				var bit = (int)field.GetRawConstantValue();

				if ( bit == 0 || (bit & (bit - 1)) != 0 || properties.ContainsKey( (uint)bit ) )
					continue;

				var flag = new Flag( targets, bit );

				properties.Add( (uint)bit, EditorTypeLibrary.CreateProperty<bool>( $"{field.Name[SpawnFlagPrefix.Length..].ToTitleCase()} ({(uint)bit})", flag.Get, flag.Set ) );
			}
		}

		return [.. properties.Values];
	}

	private static void AddProperties( List<SerializedProperty> properties, MemberDescription member, Edict[] targets )
	{
		var type = MemberType( member );

		if ( type is null )
			return;

		if ( type.IsAssignableTo( typeof( Delegate ) ) )
		{
			properties.Add( EditorTypeLibrary.CreateProperty<string>( member.Title, new Value<object>( member, targets ).Function, null, ReadOnlyAttributes ) );
			return;
		}

		if ( type.IsArray )
		{
			if ( GetValue( member, targets[0] ) is not Array array )
				return;

			var create = CreateElementMethod.MakeGenericMethod( type.GetElementType() );

			for ( var i = 0; i < array.Length; i++ )
				properties.Add( (SerializedProperty)create.Invoke( null, [member, targets, i] ) );

			return;
		}

		if ( type.IsGenericType )
		{
			properties.Add( EditorTypeLibrary.CreateProperty<string>( member.Title, new Value<object>( member, targets ).Collection, null, ReadOnlyAttributes ) );
			return;
		}

		properties.Add( (SerializedProperty)CreateMethod.MakeGenericMethod( type ).Invoke( null, [member, targets] ) );
	}

	private static SerializedProperty Create<T>( MemberDescription member, Edict[] targets )
	{
		var value = new Value<T>( member, targets );
		var writable = member is PropertyDescription { CanWrite: true } or FieldDescription { IsInitOnly: false };

		return EditorTypeLibrary.CreateProperty<T>( member.Title, value.Get, value.Set, writable ? null : ReadOnlyAttributes );
	}

	private static SerializedProperty CreateElement<T>( MemberDescription member, Edict[] targets, int index )
	{
		var element = new Element<T>( member, targets, index );

		return EditorTypeLibrary.CreateProperty<T>( $"{member.Title} [{index}]", element.Get, element.Set );
	}

	private static Type MemberType( MemberDescription member )
	{
		return member is PropertyDescription property ? property.PropertyType : (member as FieldDescription)?.FieldType;
	}

	private static object GetValue( MemberDescription member, Edict edict )
	{
		if ( !edict.IsValid() )
			return null;

		return member is PropertyDescription property ? property.GetValue( edict ) : ((FieldDescription)member).GetValue( edict );
	}

	private static void SetValue( MemberDescription member, Edict edict, object value )
	{
		if ( !edict.IsValid() || !member.TypeDescription.TargetType.IsInstanceOfType( edict ) )
			return;

		if ( member.Name == nameof( Edict.Origin ) && value is Vector3 origin )
			edict.Server.SetOrigin( edict, origin );
		else if ( member is PropertyDescription property )
			property.SetValue( edict, value );
		else
			((FieldDescription)member).SetValue( edict, value );
	}

	private sealed class Value<T>( MemberDescription member, Edict[] targets )
	{
		public T Get() => GetValue( member, targets[0] ) is T value ? value : default;

		public void Set( T value )
		{
			foreach ( var target in targets )
				SetValue( member, target, value );
		}

		public string Function() => (GetValue( member, targets[0] ) as Delegate)?.Method.Name ?? "";

		public string Collection()
		{
			return GetValue( member, targets[0] ) is System.Collections.IEnumerable items ? string.Join( ", ", items.Cast<object>() ) : "";
		}
	}

	private sealed class Element<T>( MemberDescription member, Edict[] targets, int index )
	{
		public T Get() => GetValue( member, targets[0] ) is T[] array && index < array.Length ? array[index] : default;

		public void Set( T value )
		{
			foreach ( var target in targets )
			{
				if ( GetValue( member, target ) is T[] array && index < array.Length )
					array[index] = value;
			}
		}
	}

	private sealed class Use( Edict[] targets, UseType useType )
	{
		public void Fire()
		{
			foreach ( var target in targets )
			{
				if ( target.IsValid() && target is BaseEntity entity && target.Server.Player is BaseEntity player )
					entity.Use( player, player, useType, 0f );
			}
		}
	}

	private sealed class Flag( Edict[] targets, int bit )
	{
		public bool Get() => targets[0].IsValid() && (targets[0].SpawnFlags & bit) != 0;

		public void Set( bool value )
		{
			foreach ( var target in targets )
			{
				if ( target.IsValid() )
					target.SpawnFlags = value ? target.SpawnFlags | bit : target.SpawnFlags & ~bit;
			}
		}
	}
}

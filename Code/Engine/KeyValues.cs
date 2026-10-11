using System;

namespace GoldSrc;

public sealed class KeyValues( string name )
{
	private readonly List<KeyValues> _keys = [];

	public string Name { get; } = name;

	public string Value { get; private set; }

	public bool IsInt { get; private set; }

	public IReadOnlyList<KeyValues> Keys => _keys;

	public KeyValues FindKey( string name )
	{
		return _keys.Find( x => x.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) );
	}

	public KeyValues FindKey( string name, bool create )
	{
		var key = FindKey( name );

		if ( key is null && create )
		{
			key = new KeyValues( name );

			_keys.Add( key );
		}

		return key;
	}

	public void AddSubKey( KeyValues key ) => _keys.Add( key );

	public void RemoveSubKey( KeyValues key ) => _keys.Remove( key );

	public void Clear()
	{
		_keys.Clear();

		Value = null;
	}

	public KeyValues FindPath( string path )
	{
		var key = this;

		foreach ( var name in path.Split( '/' ) )
		{
			key = key.FindKey( name );

			if ( key is null )
				return null;
		}

		return key;
	}

	public void SetString( string name, string value )
	{
		var key = FindKey( name );

		if ( key is null )
		{
			key = new KeyValues( name );

			_keys.Add( key );
		}

		key.Value = value;
		key.IsInt = false;
	}

	public void SetInt( string name, int value )
	{
		SetString( name, value.ToString() );

		FindKey( name ).IsInt = true;
	}

	public KeyValues MakeCopy()
	{
		var copy = new KeyValues( Name ) { Value = Value, IsInt = IsInt };

		foreach ( var key in _keys )
			copy._keys.Add( key.MakeCopy() );

		return copy;
	}

	public string GetString( string name, string defaultValue = "" ) => FindKey( name )?.Value ?? defaultValue;

	public int GetInt( string name, int defaultValue = 0 )
	{
		return FindKey( name )?.Value is { } value ? Common.Atoi( value ) : defaultValue;
	}

	public static KeyValues LoadFromFile( string fileName )
	{
		return Server.LoadMountText( fileName ) is { } text ? LoadFromText( text ) : null;
	}

	public static KeyValues LoadFromText( string text )
	{
		var position = 0;

		if ( Common.Parse( text, ref position ) is not { } name )
			return null;

		var root = new KeyValues( name );

		if ( Common.Parse( text, ref position ) == "{" )
			root.Load( text, ref position );

		return root;
	}

	private void Load( string text, ref int position )
	{
		while ( Common.Parse( text, ref position ) is { } name && name != "}" )
		{
			var key = new KeyValues( name );

			_keys.Add( key );

			if ( Common.Parse( text, ref position ) is not { } value )
				return;

			if ( value == "{" )
				key.Load( text, ref position );
			else
				key.Value = value;
		}
	}

	public string SaveToText()
	{
		var text = new System.Text.StringBuilder();

		Save( text, 0 );

		return text.ToString();
	}

	private void Save( System.Text.StringBuilder text, int indentLevel )
	{
		text.Append( '\t', indentLevel ).Append( '"' ).Append( Name ).Append( "\"\n" );
		text.Append( '\t', indentLevel ).Append( "{\n" );

		foreach ( var key in _keys )
		{
			if ( key._keys.Count > 0 || key.Value is null )
				key.Save( text, indentLevel + 1 );
			else
				text.Append( '\t', indentLevel + 1 ).Append( '"' ).Append( key.Name ).Append( "\"\t\t\"" ).Append( key.Value ).Append( "\"\n" );
		}

		text.Append( '\t', indentLevel ).Append( "}\n" );
	}
}

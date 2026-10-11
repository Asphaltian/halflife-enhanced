using System;
using System.Globalization;
using System.Text;

namespace GoldSrc.GameUI;

public enum ScriptObjectType
{
	Unknown,
	Bool,
	Number,
	List,
	String
}

public sealed class ScriptListItem( string itemText, string value )
{
	public string ItemText = ScriptObject.Truncate( itemText, ScriptObject.MaxValueLength );
	public string Value = ScriptObject.Truncate( value, ScriptObject.MaxValueLength );
}

public sealed class ScriptObject
{
	public const int MaxValueLength = 128;

	[SkipHotload]
	private static readonly (ScriptObjectType Type, string Name)[] Types =
	[
		(ScriptObjectType.Bool, "BOOL"),
		(ScriptObjectType.Number, "NUMBER"),
		(ScriptObjectType.List, "LIST"),
		(ScriptObjectType.String, "STRING")
	];

	public ScriptObjectType Type = ScriptObjectType.Bool;
	public string CvarName = "";
	public string Prompt = "";
	public readonly List<ScriptListItem> ListItems = [];
	public float Min;
	public float Max;
	public string DefValue = "";
	public float DefValueFloat;
	public string CurValue = "";
	public float CurValueFloat;
	public bool SetInfo;

	public static string Truncate( string value, int size ) => value.Length < size ? value : value[..(size - 1)];

	public static string StripInvalidCharacters( string value )
	{
		var builder = new StringBuilder( value.Length );

		foreach ( var character in value )
		{
			if ( character != '"' && character != '%' )
				builder.Append( character );
		}

		return builder.ToString();
	}

	public static string StripFloatTrailingZeros( string value )
	{
		var period = value.IndexOf( '.' );

		if ( period < 0 )
			return value;

		var end = value.Length - 1;

		while ( period < end && value[end] == '0' )
			end--;

		return value[end] == '.' ? value[..end] : value[..(end + 1)];
	}

	public void SetCurValue( string value )
	{
		CurValue = Truncate( value, MaxValueLength );
		CurValueFloat = (float)Common.Strtod( CurValue );

		if ( Type is ScriptObjectType.Number or ScriptObjectType.Bool )
			CurValue = StripFloatTrailingZeros( CurValue );
	}

	public ScriptObject Clone()
	{
		var copy = new ScriptObject
		{
			Type = Type,
			CvarName = CvarName,
			Prompt = Prompt,
			Min = Min,
			Max = Max,
			DefValue = DefValue,
			DefValueFloat = DefValueFloat,
			CurValue = CurValue,
			CurValueFloat = CurValueFloat,
			SetInfo = SetInfo
		};

		foreach ( var item in ListItems )
			copy.ListItems.Add( new ScriptListItem( item.ItemText, item.Value ) );

		return copy;
	}

	private static bool Token( string text, ref int position, out string token )
	{
		token = Common.Parse( text, ref position ) ?? "";

		return token.Length > 0;
	}

	private static bool Expect( string token, string expected )
	{
		if ( token == expected )
			return true;

		Con.Printf( $"Expecting '{{', got '{token}'" );

		return false;
	}

	public bool ReadFromBuffer( string text, ref int position )
	{
		if ( !Token( text, ref position, out var token ) )
			return false;

		CvarName = token;

		if ( !Token( text, ref position, out token ) || !Expect( token, "{" ) )
			return false;

		if ( !Token( text, ref position, out token ) )
			return false;

		Prompt = token;

		if ( !Token( text, ref position, out token ) || !Expect( token, "{" ) )
			return false;

		if ( !Token( text, ref position, out token ) )
			return false;

		Type = ScriptObjectType.Unknown;

		foreach ( var type in Types )
		{
			if ( type.Name.Equals( token, StringComparison.OrdinalIgnoreCase ) )
			{
				Type = type.Type;
				break;
			}
		}

		if ( Type == ScriptObjectType.Unknown )
		{
			Con.Printf( $"Type '{token}' unknown" );
			return false;
		}

		switch ( Type )
		{
			case ScriptObjectType.Bool:
			case ScriptObjectType.String:
				if ( !Token( text, ref position, out token ) || !Expect( token, "}" ) )
					return false;

				break;

			case ScriptObjectType.Number:
				if ( !Token( text, ref position, out token ) )
					return false;

				Min = (float)Common.Strtod( token );

				if ( !Token( text, ref position, out token ) )
					return false;

				Max = (float)Common.Strtod( token );

				if ( !Token( text, ref position, out token ) || !Expect( token, "}" ) )
					return false;

				break;

			case ScriptObjectType.List:
				while ( true )
				{
					if ( !Token( text, ref position, out token ) )
						return false;

					if ( token == "}" )
						break;

					var itemText = token;

					if ( !Token( text, ref position, out token ) )
						return false;

					ListItems.Add( new ScriptListItem( itemText, token ) );
				}

				break;
		}

		if ( !Token( text, ref position, out token ) || !Expect( token, "{" ) )
			return false;

		token = Common.Parse( text, ref position ) ?? "";

		DefValue = Truncate( token, MaxValueLength );
		DefValueFloat = (float)Common.Strtod( token );

		if ( Type == ScriptObjectType.Number )
			DefValue = StripFloatTrailingZeros( DefValue );

		SetCurValue( DefValue );

		if ( !Token( text, ref position, out token ) || !Expect( token, "}" ) )
			return false;

		if ( !Token( text, ref position, out token ) )
			return false;

		if ( token.Equals( "SetInfo", StringComparison.OrdinalIgnoreCase ) )
		{
			SetInfo = true;

			if ( !Token( text, ref position, out token ) )
				return false;
		}

		return Expect( token, "}" );
	}

	private static string Float( float value ) => ((double)value).ToString( "F6", CultureInfo.InvariantCulture );

	public void WriteToScriptFile( StringBuilder file )
	{
		CvarName = StripInvalidCharacters( CvarName );

		file.Append( $"\t\"{CvarName}\"\r\n" );
		file.Append( "\t{\r\n" );

		Prompt = StripInvalidCharacters( Prompt );

		switch ( Type )
		{
			case ScriptObjectType.Bool:
				file.Append( $"\t\t\"{Prompt}\"\r\n" );
				file.Append( "\t\t{ BOOL }\r\n" );
				file.Append( $"\t\t{{ \"{((int)CurValueFloat != 0 ? 1 : 0)}\" }}\r\n" );
				break;

			case ScriptObjectType.Number:
				file.Append( $"\t\t\"{Prompt}\"\r\n" );
				file.Append( $"\t\t{{ NUMBER {Float( Min )} {Float( Max )} }}\r\n" );
				file.Append( $"\t\t{{ \"{Float( CurValueFloat )}\" }}\r\n" );
				break;

			case ScriptObjectType.List:
				file.Append( $"\t\t\"{Prompt}\"\r\n" );
				file.Append( "\t\t{\r\n\t\t\tLIST\r\n" );

				foreach ( var item in ListItems )
				{
					item.ItemText = StripInvalidCharacters( item.ItemText );
					item.Value = StripInvalidCharacters( item.Value );

					file.Append( $"\t\t\t\"{item.ItemText}\" \"{item.Value}\"\r\n" );
				}

				file.Append( "\t\t}\r\n" );
				file.Append( $"\t\t{{ \"{Float( CurValueFloat )}\" }}\r\n" );
				break;

			case ScriptObjectType.String:
				file.Append( $"\t\t\"{Prompt}\"\r\n" );
				file.Append( "\t\t{ STRING }\r\n" );

				CurValue = StripInvalidCharacters( CurValue );

				file.Append( $"\t\t{{ \"{CurValue}\" }}\r\n" );
				break;
		}

		if ( SetInfo )
			file.Append( "\t\tSetInfo\r\n" );

		file.Append( "\t}\r\n\r\n" );
	}

	public void WriteToConfig()
	{
		string value;

		switch ( Type )
		{
			case ScriptObjectType.Bool:
				value = CurValueFloat != 0f ? "1" : "0";
				break;

			case ScriptObjectType.Number:
				{
					var number = CurValueFloat;

					if ( Min != -1f && !(number > Min) )
						number = Min;

					if ( Max != -1f && !(Max > number) )
						number = Max;

					value = Float( number );
					break;
				}

			case ScriptObjectType.String:
				value = StripInvalidCharacters( $"\"{CurValue}\"" );
				break;

			case ScriptObjectType.List:
				{
					var index = (int)CurValueFloat;

					value = index < ListItems.Count ? StripInvalidCharacters( ListItems[Math.Max( index, 0 )].Value ) : "0.0";
					break;
				}

			default:
				return;
		}

		Client.ClientCmd( SetInfo ? $"setinfo {CvarName} \"{value}\"\n" : $"{CvarName} \"{value}\"\n" );
	}
}

public class Description
{
	private const float ScriptVersion = 1f;

	public readonly List<ScriptObject> Objects = [];

	public string Hint = "";
	public string DescriptionName = "";

	public bool ReadFromBuffer( string text )
	{
		var position = 0;
		var token = Common.Parse( text, ref position ) ?? "";

		if ( token.Length == 0 )
			return false;

		if ( !token.Equals( "VERSION", StringComparison.OrdinalIgnoreCase ) )
		{
			Con.Printf( $"Expecting 'VERSION', got '{token}'" );
			return false;
		}

		token = Common.Parse( text, ref position ) ?? "";

		if ( token.Length == 0 )
		{
			Con.Printf( "Expecting version #" );
			return false;
		}

		var version = (float)Common.Strtod( token );

		if ( version != ScriptVersion )
		{
			Con.Printf( $"Version mismatch, expecting {(double)ScriptVersion:F6}, got {(double)version:F6}" );
			return false;
		}

		token = Common.Parse( text, ref position ) ?? "";

		if ( token.Length == 0 )
			return false;

		if ( !token.Equals( "DESCRIPTION", StringComparison.OrdinalIgnoreCase ) )
		{
			Con.Printf( $"Expecting 'DESCRIPTION', got '{token}'" );
			return false;
		}

		token = Common.Parse( text, ref position ) ?? "";

		if ( token.Length == 0 )
		{
			Con.Printf( $"Expecting '{DescriptionName}'" );
			return false;
		}

		if ( !token.Equals( DescriptionName, StringComparison.OrdinalIgnoreCase ) )
		{
			Con.Printf( $"Expecting {DescriptionName}, got {token}" );
			return false;
		}

		token = Common.Parse( text, ref position ) ?? "";

		if ( token.Length == 0 )
			return false;

		if ( token != "{" )
		{
			Con.Printf( $"Expecting '{{', got '{token}'" );
			return false;
		}

		while ( true )
		{
			var start = position;

			token = Common.Parse( text, ref position ) ?? "";

			if ( token.Length == 0 )
				return false;

			if ( token.Equals( "}", StringComparison.OrdinalIgnoreCase ) )
				return true;

			position = start;

			var scriptObject = new ScriptObject();

			if ( !scriptObject.ReadFromBuffer( text, ref position ) )
				return false;

			Objects.Add( scriptObject );
		}
	}

	protected static string ReadScript( string fileName )
	{
		return FileSystem.Data.FileExists( fileName ) ? FileSystem.Data.ReadAllText( fileName ) : Server.LoadMountText( fileName );
	}

	public virtual bool InitFromFile( string fileName )
	{
		if ( ReadScript( fileName ) is not { } text )
			return false;

		ReadFromBuffer( text );

		return true;
	}

	public void TransferCurrentValues()
	{
		foreach ( var scriptObject in Objects )
		{
			var value = scriptObject.SetInfo
				? Info.ValueForKey( Client.UserInfoString(), scriptObject.CvarName )
				: Cvar.GetString( scriptObject.CvarName );

			if ( string.IsNullOrEmpty( value ) )
				continue;

			scriptObject.CurValue = ScriptObject.Truncate( value, ScriptObject.MaxValueLength );
			scriptObject.CurValueFloat = (float)Common.Strtod( value );
			scriptObject.DefValue = scriptObject.CurValue;
			scriptObject.DefValueFloat = scriptObject.CurValueFloat;
		}
	}

	public void WriteToConfig()
	{
		foreach ( var scriptObject in Objects )
			scriptObject.WriteToConfig();
	}

	protected virtual void WriteScriptHeader( StringBuilder file )
	{
	}

	public void WriteToScriptFile( StringBuilder file )
	{
		WriteScriptHeader( file );

		foreach ( var scriptObject in Objects )
			scriptObject.WriteToScriptFile( file );

		file.Append( "}\r\n" );
	}
}

public sealed class InfoDescription : Description
{
	public const string DefaultHint =
		"// NOTE:  THIS FILE IS AUTOMATICALLY REGENERATED, \r\n" +
		"//DO NOT EDIT THIS HEADER, YOUR COMMENTS WILL BE LOST IF YOU DO\r\n" +
		"// User options script\r\n" +
		"//\r\n" +
		"// Format:\r\n" +
		"//  Version [float]\r\n" +
		"//  Options description followed by \r\n" +
		"//  Options defaults\r\n" +
		"//\r\n" +
		"// Option description syntax:\r\n" +
		"//\r\n" +
		"//  \"cvar\" { \"Prompt\" { type [ type info ] } { default } }\r\n" +
		"//\r\n" +
		"//  type = \r\n" +
		"//   BOOL   (a yes/no toggle)\r\n" +
		"//   STRING\r\n" +
		"//   NUMBER\r\n" +
		"//   LIST\r\n" +
		"//\r\n" +
		"// type info:\r\n" +
		"// BOOL                 no type info\r\n" +
		"// NUMBER       min max range, use -1 -1 for no limits\r\n" +
		"// STRING       no type info\r\n" +
		"// LIST          delimited list of options value pairs\r\n" +
		"//\r\n" +
		"//\r\n" +
		"// default depends on type\r\n" +
		"// BOOL is \"0\" or \"1\"\r\n" +
		"// NUMBER is \"value\"\r\n" +
		"// STRING is \"value\"\r\n" +
		"// LIST is \"index\", where index \"0\" is the first element of the list\r\n" +
		"\r\n" +
		"\r\n";

	public InfoDescription()
	{
		Hint = DefaultHint;
		DescriptionName = "INFO_OPTIONS";
	}

	public override bool InitFromFile( string fileName )
	{
		var result = base.InitFromFile( fileName );

		if ( ModInfo.GameName.Equals( "Condition Zero", StringComparison.OrdinalIgnoreCase ) )
		{
			foreach ( var scriptObject in Objects )
			{
				if ( scriptObject.CvarName.Equals( "cl_tutor", StringComparison.OrdinalIgnoreCase ) )
					scriptObject.CvarName = "tutor_enable";
			}
		}

		var defaults = new InfoDescription();

		if ( ReadScript( "user_default.scr" ) is { } text )
			defaults.ReadFromBuffer( text );

		foreach ( var scriptObject in defaults.Objects )
		{
			if ( !Objects.Exists( x => x.CvarName.Equals( scriptObject.CvarName, StringComparison.OrdinalIgnoreCase ) ) )
				Objects.Add( scriptObject.Clone() );
		}

		return result;
	}

	protected override void WriteScriptHeader( StringBuilder file )
	{
		var time = DateTime.Now;

		file.Append( Hint );
		file.Append( "// Half-Life User Info Configuration Layout Script (stores last settings chosen, too)\r\n" );
		file.Append( $"// File generated:  {time.ToString( "ddd MMM dd HH:mm:ss", CultureInfo.InvariantCulture )} AM\r\n" );
		file.Append( "//\r\n//\r\n// Cvar\t-\tSetting\r\n\r\n" );
		file.Append( "VERSION 1.0\r\n\r\n" );
		file.Append( "DESCRIPTION INFO_OPTIONS\r\n{\r\n" );
	}
}

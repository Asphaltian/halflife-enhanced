using System;
using Sandbox.Mounting;

namespace GoldSrc;

partial class Server
{
	public static bool FileExists( string fileName )
	{
		return FileSystem.Data.FileExists( fileName ) || MountFileExists( GetMountPath( fileName ) );
	}

	public static byte[] ReadFile( string fileName )
	{
		if ( FileSystem.Data.FileExists( fileName ) )
			return FileSystem.Data.ReadAllBytes( fileName ).ToArray();

		return LoadMountFile( fileName ) is { } data ? data.AsSpan().ToArray() : null;
	}

	public static byte[] ReadRootFile( string fileName )
	{
		var parts = fileName.Split( ['/', '\\'], 2, StringSplitOptions.RemoveEmptyEntries );

		if ( parts.Length < 2 )
			return null;

		if ( parts[0].Equals( GameDirectory, StringComparison.OrdinalIgnoreCase ) && FileSystem.Data.FileExists( parts[1] ) )
			return FileSystem.Data.ReadAllBytes( parts[1] ).ToArray();

		var path = string.Join( '/', parts[1].Split( ['/', '\\'], StringSplitOptions.RemoveEmptyEntries ) );
		var task = MountUtility.FindLoader( $"mount://{MountIdent}/{parts[0]}/{path}" )?.GetOrCreate();

		return task is { IsCompletedSuccessfully: true } && task.Result is byte[] data ? data.AsSpan().ToArray() : null;
	}

	public static bool WriteRootFile( string fileName, byte[] data )
	{
		var parts = fileName.Split( ['/', '\\'], 2, StringSplitOptions.RemoveEmptyEntries );

		if ( parts.Length < 2 || !parts[0].Equals( GameDirectory, StringComparison.OrdinalIgnoreCase ) )
			return false;

		WriteFile( parts[1], data );

		return true;
	}

	public static void AppendFile( string fileName, string text )
	{
		using var stream = FileSystem.Data.OpenWrite( fileName, System.IO.FileMode.Append );

		stream.Write( System.Text.Encoding.Latin1.GetBytes( text ) );
	}

	public static void RemoveFile( string fileName )
	{
		if ( FileSystem.Data.FileExists( fileName ) )
			FileSystem.Data.DeleteFile( fileName );
	}

	public static void WriteFile( string fileName, byte[] data )
	{
		var directory = System.IO.Path.GetDirectoryName( fileName );

		if ( !string.IsNullOrEmpty( directory ) )
			FileSystem.Data.CreateDirectory( directory );

		FileSystem.Data.WriteAllBytes( fileName, data );
	}

	public static List<string> FindFiles( string directory, string extension )
	{
		return Find( directory, name => !name.Contains( '/' ) && name.EndsWith( extension, StringComparison.OrdinalIgnoreCase ) ? name : null );
	}

	public static List<string> FindDirectories( string directory )
	{
		return Find( directory, name => name.IndexOf( '/' ) is > 0 and var slash ? name[..slash] : null );
	}

	private static List<string> Find( string directory, Func<string, string> select )
	{
		var found = new List<string>();

		if ( Sandbox.Mounting.Directory.Get( MountIdent ) is not { } mount )
			return found;

		foreach ( var searchPath in _searchPaths )
		{
			var prefix = $"mount://{MountIdent}/{searchPath}/{directory}/";
			var names = new SortedSet<string>( StringComparer.OrdinalIgnoreCase );

			foreach ( var resource in mount.Resources )
			{
				if ( resource.Type != ResourceType.Binary || !resource.Path.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
					continue;

				if ( select( resource.Path[prefix.Length..] ) is { } name )
					names.Add( name );
			}

			found.AddRange( names );
		}

		return found;
	}
}

using System;

namespace GoldSrc;

public static class ModelCache
{
	private const int StudioId = 0x54534449;

	private static Dictionary<string, Model> _known = new( StringComparer.OrdinalIgnoreCase );

	public static void Clear()
	{
		foreach ( var model in _known.Values )
		{
			if ( model is not BrushModel brush )
				continue;

			foreach ( var hull in brush.Hulls )
				hull.Dispose();
		}

		_known.Clear();
	}

	public static void Add( Model model ) => _known[model.Name] = model;

	public static Model ForName( string name )
	{
		if ( _known.TryGetValue( name, out var model ) )
			return model;

		model = Load( name );

		if ( model is not null )
			_known[name] = model;

		return model;
	}

	private static Model Load( string name )
	{
		if ( Server.LoadMountFile( name ) is not { } data )
			return null;

		var id = BitConverter.ToInt32( data );

		if ( id == SpriteModel.Id )
			return SpriteModel.Load( name, data, Server.GetMountPath( name ) );

		if ( id == StudioId )
		{
			var renderModel = Sandbox.Model.Load( $"{Server.GetMountPath( name )}.vmdl" );

			return new StudioModel
			{
				Name = name,
				File = new Studio.File( name, data, Server.LoadMountFile, renderModel ),
				RenderModel = renderModel
			};
		}

		var models = BrushModel.Load( new Bsp.File( data ), name, Server.GetMountPath( name ) );

		foreach ( var brush in models )
			_known[brush.Name] = brush;

		return models[0];
	}
}

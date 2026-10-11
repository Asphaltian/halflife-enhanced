using System;

namespace GoldSrc;

partial class Client
{
	private const string SkyDirectory = "gfx/env";
	private const string SkyTexturePrefix = "sky";
	private const string DefaultSky = "desert";
	private const string ModelExtension = ".vmdl";

	private GameObject _sky;

	private void ClearSky()
	{
		_sky?.Destroy();
		_sky = null;
	}

	private void LoadSky()
	{
		ClearSky();

		if ( !Array.Exists( WorldFile.TextureNames, x => x is not null && x.StartsWith( SkyTexturePrefix, StringComparison.Ordinal ) ) )
			return;

		var name = _moveVars.SkyName ?? "";
		var path = Server.FindMountPath( $"{SkyDirectory}/{name}", ModelExtension );

		if ( path is null )
		{
			Con.Printf( $"R_LoadSkys: Couldn't load {SkyDirectory}/{name}rt.tga\n" );

			if ( name == DefaultSky )
				return;

			_moveVars.SkyName = DefaultSky;

			path = Server.FindMountPath( $"{SkyDirectory}/{DefaultSky}", ModelExtension );

			if ( path is null )
			{
				Con.Printf( $"R_LoadSkys: Couldn't load {SkyDirectory}/{DefaultSky}rt.tga\n" );
				return;
			}
		}

		_sky = new GameObject( true, "sky" ) { NetworkMode = NetworkMode.Never };

		var renderer = _sky.AddComponent<ModelRenderer>();

		renderer.Model = Sandbox.Model.Load( $"{path}{ModelExtension}" );
		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
	}
}

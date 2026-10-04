HEADER
{
	Description = "GoldSrc triangle API";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
}

COMMON
{
	#include "common/shared.hlsl"

	#define CUSTOM_MATERIAL_INPUTS
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"

	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vVertexColor = v.vColor;
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"

	DynamicCombo( D_TRI_BLEND, 0..2, Sys( ALL ) );
	DynamicCombo( D_TRI_DEPTH_WRITE, 0..1, Sys( ALL ) );
	DynamicCombo( D_TRI_CULL, 0..1, Sys( ALL ) );
	DynamicCombo( D_TRI_ALPHA_TEST, 0..1, Sys( ALL ) );
	DynamicCombo( D_GAMMA_SPACE, 0..1, Sys( ALL ) );

	RenderState( BlendEnable, true );

	#if D_TRI_BLEND == 2
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, ONE );
	#elif D_TRI_BLEND == 1
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, INV_SRC_ALPHA );
	#else
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, ZERO );
	#endif

	#if D_GAMMA_SPACE
		RenderState( SrcBlendAlpha, ZERO );
		#if D_TRI_BLEND == 0
			RenderState( DstBlendAlpha, ZERO );
		#else
			RenderState( DstBlendAlpha, ONE );
		#endif
	#endif

	#if !D_TRI_DEPTH_WRITE
		RenderState( DepthWriteEnable, false );
	#endif

	#if !D_TRI_CULL
		RenderState( CullMode, NONE );
	#endif

	SamplerState g_sColorSampler < Filter( Anisotropic ); MaxAniso( 16 ); AddressU( WRAP ); AddressV( WRAP ); >;

	CreateInputTexture2D( Color, Linear, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( false ); >;

	float3 GammaToLinear( float3 color )
	{
		float3 low = color / 12.92;
		float3 high = pow( ( color + 0.055 ) / 1.055, 2.4 );
		return lerp( low, high, step( 0.04045, color ) );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float4 color = Tex2DS( g_tColor, g_sColorSampler, i.vTextureCoords.xy ) * i.vVertexColor;

		#if D_TRI_ALPHA_TEST
			if ( color.a == 0.0 )
				discard;
		#endif

		#if D_GAMMA_SPACE
			return color;
		#else
			return float4( GammaToLinear( color.rgb ), color.a );
		#endif
	}
}

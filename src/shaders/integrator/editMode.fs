#version 430

#include <../focalDistance/focalDistanceDevice.h>
#include <../editVoxels/selectVoxelDevice.h>

uniform sampler3D   occupancyTexture;
uniform sampler3D   voxelColorTexture;
uniform sampler2D   noiseTexture;
uniform ivec3       voxelResolution;
uniform vec3        volumeBoundsMin;
uniform vec3        volumeBoundsMax;

uniform vec4        viewport;
uniform float       cameraNear;
uniform float       cameraFar;
uniform mat4        cameraProj;
uniform mat4        cameraInverseProj;
uniform mat4        cameraInverseModelView;
uniform float       cameraFocalLength;
uniform float       cameraLensRadius;
uniform vec2        cameraFilmSize;

uniform vec4        backgroundColorSky = vec4(153.0 / 255, 187.0 / 255, 201.0 / 255, 1) * 2;
uniform vec4        backgroundColorGround = vec4(77.0 / 255, 64.0 / 255, 50.0 / 255, 1);
uniform vec3        groundColor = vec3(0.5, 0.5, 0.5);

uniform int         sampleCount;
uniform int         enableDOF;
uniform int			pathtracerMaxPathLength;

uniform float		wireframeOpacity = 0;
uniform float		wireframeThickness = 0.01;

uniform vec3		lightDirection = vec3(1, -1, -1);
uniform float		ambientLight = 0.5;
out vec4 outColor;

#include <../shared/aabb.h>
#include <../shared/coordinates.h>
#include <../shared/dda.h>
#include <../shared/sampling.h>
#include <../shared/random.h>
#include <../shared/generateRay.h>
#include <../shared/bsdf.h>
#include <../shared/lights.h>

float ISECT_EPSILON = 0.01;

void generateRay(inout ivec2 rngOffset, out vec3 wsRayOrigin, out vec3 wsRayDir)
{
	if (enableDOF != 0)
	{
		generateRay_ThinLens(gl_FragCoord.xyz, rngOffset, wsRayOrigin, wsRayDir);
	}
	else
	{
		generateRay_Pinhole(gl_FragCoord.xyz, wsRayOrigin, wsRayDir);
	}

}


void main()
{
	ivec2 rngOffset = randomNumberGeneratorOffset(ivec4(gl_FragCoord), sampleCount);

	vec3 wsRayOrigin;
	vec3 wsRayDir;
	generateRay_Pinhole(gl_FragCoord.xyz, wsRayOrigin, wsRayDir);

	// test intersection with bounds to trivially discard rays before entering
	// traversal.
	float aabbIsectDist = rayAABBIntersection(wsRayOrigin, wsRayDir,
											  volumeBoundsMin, volumeBoundsMax); 

	bool hitGround;
	if (aabbIsectDist < 0)
	{
		// we're not even hitting the volume's bounding box. Early out.
		outColor = vec4(getBackgroundColor(wsRayDir),1);
		return;
	}

	float rayLength = aabbIsectDist;

	// push the intersection slightly inside the hit voxel so that when we cast 
	// to a voxel index we don't mistakenly take an adjacent voxel. This is 
	// important to ensure the traversal starts inside of the volume bounds.
	vec3 halfVoxellDist = 0*sign(wsRayDir) * 0.5 / voxelResolution; 
	vec3 wsRayEntryPoint = wsRayOrigin + rayLength * wsRayDir + halfVoxellDist;

	vec3 vsHitPos;

	// Cast primary ray
	if ( !traverse(wsRayEntryPoint, wsRayDir, vsHitPos, hitGround) )
	{
		outColor = vec4(getBackgroundColor(wsRayDir),1);
		return;
	}

	// convert hit position from voxel space to world space. We also use the
	// calculations to generate a world-space basis 
	// <wsHitTangent, wsHitNormal, wsHitBinormal> which we'll use for the 
	// local<->world space conversions.
	Basis wsHitBasis;
	voxelSpaceToWorldSpace(vsHitPos, 
						   wsRayOrigin, wsRayDir,
						   wsHitBasis);
	
	vec3 albedo = hitGround ? 
				groundColor :
				texelFetch(voxelColorTexture,
							 ivec3(vsHitPos.x, vsHitPos.y, vsHitPos.z), 0).xyz;

	// Wireframe overlay
	if (wireframeOpacity > 0)
	{
		vec3 vsVoxelCenter = (wsHitBasis.position - volumeBoundsMin) / (volumeBoundsMax - volumeBoundsMin) * voxelResolution;
		vec3 uvw = vsHitPos - vsVoxelCenter;
		vec2 uv = abs(vec2(dot(wsHitBasis.normal.yzx, uvw), dot( wsHitBasis.normal.zxy, uvw)));
		float wireframe = step(wireframeThickness, uv.x) * step(uv.x, 1-wireframeThickness) *
						  step(wireframeThickness, uv.y) * step(uv.y, 1-wireframeThickness);

		wireframe = (1-wireframeOpacity) + wireframeOpacity * wireframe;	
		albedo *= vec3(wireframe);
	}

	if ( ivec3(vsHitPos) == SelectVoxelData.index.xyz )
	{
		// Draw selected voxel as red
		albedo = vec3(1,0,0); 
	}

	// dot normal lighting
	float lighting = max(0, dot(-wsRayDir, wsHitBasis.normal));
lighting = sqrt(lighting);
	// trace shadow ray
	if ( traverse(wsHitBasis.position -lightDirection * ISECT_EPSILON, -lightDirection, vsHitPos, hitGround) )
	{
		lighting *= ambientLight;
	}

	vec3 radiance = albedo * lighting; 
	
	outColor = vec4(radiance,1);
}



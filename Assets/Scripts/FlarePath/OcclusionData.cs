using System;
using ModApi.Craft;
using ModApi.Craft.Parts;
using UnityEngine;

public class OcclusionData : IComparable<OcclusionData>
{
    public IPartScript part;
    public PartThermalData ptd;

    // Projected geometry properties
    public double projectedArea;
    public double projectedRadius;
    public double invFineness;
    public double minimumDot;
    public double maximumDot;
    public double centroidDot;
    public double maxWidthDepth;

    // Bounding box information
    public Vector3 boundsCenter;
    public Vector3[] boundsVertices = new Vector3[8];
    public Vector3 projectedCenter;
    public Vector3[] projectedVertices = new Vector3[8];
    public float[] projectedDots = new float[8];

    // 2D projection bounds
    public Vector2 center;
    public Vector2 minExtents;
    public Vector2 maxExtents;
    public Vector2 extents;

    // Occlusion objects
    public OcclusionCone convCone;
    public OcclusionCylinder sunCyl;
    public OcclusionCylinder bodyCyl;

    public OcclusionData(PartThermalData data)
    {
        ptd = data;
        part = ptd.part;
        convCone = new OcclusionCone();
        sunCyl = new OcclusionCylinder();
        bodyCyl = new OcclusionCylinder();
    }

    protected void CreateCornerArray()
    {
        Vector3 GetWeightedCenter()
        {
            Vector3 totalCenter = Vector3.zero;
            float totalArea = 0f;
    
            // 遍历所有6个方向
            for (int i = 0; i < 6; i++)
            {
                Drag.DragDirection direction = (Drag.DragDirection)i;
                float area = part.Data.PartDrag.GetArea(direction);
                Vector3 center = part.Data.PartDrag.GetCenterOfDrag(direction);
        
                totalCenter += center * area;
                totalArea += area;
            }
    
            if (totalArea > 0)
                return totalCenter / totalArea;
            else
                return Vector3.zero;
        }
        
        Vector3 GetWeightedSize()
        {
            return Vector3.one;
        }
        
        
        Bounds bounds = new Bounds(GetWeightedCenter(),GetWeightedSize());
        boundsCenter = bounds.center;
        
        // Define all 8 corners of the bounding box
        boundsVertices[0] = bounds.min;
        boundsVertices[1] = bounds.max;
        boundsVertices[2] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
        boundsVertices[3] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
        boundsVertices[4] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
        boundsVertices[5] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
        boundsVertices[6] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
        boundsVertices[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
    }
    
    


    public void Update(Vector3 velocity, bool useDragArea = true)
    {
        if (part == null || part.GameObject.transform == null)
            return;

        CreateCornerArray();
        
        Matrix4x4 localToWorldMatrix = part.GameObject.transform.localToWorldMatrix;
        
        // Calculate centroid projection
        Vector3 worldCenter = localToWorldMatrix.MultiplyPoint3x4(boundsCenter);
        centroidDot = Vector3.Dot(worldCenter, velocity);
        projectedCenter = worldCenter - (float)centroidDot * velocity;

        // Rotation to align velocity with up direction
        Quaternion rotation = Quaternion.FromToRotation(velocity, Vector3.up);
        
        // Initialize bounds
        minimumDot = double.MaxValue;
        maximumDot = double.MinValue;
        minExtents = new Vector2(float.MaxValue, float.MaxValue);
        maxExtents = new Vector2(float.MinValue, float.MinValue);

        // Process each corner
        for (int i = 0; i < 8; i++)
        {
            Vector3 worldVertex = localToWorldMatrix.MultiplyPoint3x4(boundsVertices[i]);
            double dotProduct = Vector3.Dot(worldVertex, velocity);
            
            // Project vertex onto plane perpendicular to velocity
            projectedVertices[i] = worldVertex - (float)dotProduct * velocity;
            projectedDots[i] = (float)dotProduct;

            // Update min/max dot products
            if (dotProduct < minimumDot)
                minimumDot = dotProduct;
            if (dotProduct > maximumDot)
                maximumDot = dotProduct;

            // Calculate 2D extents in rotated space
            Vector3 rotatedVertex = rotation * worldVertex;
            maxExtents.x = Math.Max(maxExtents.x, rotatedVertex.x);
            maxExtents.y = Math.Max(maxExtents.y, rotatedVertex.z);
            minExtents.x = Math.Min(minExtents.x, rotatedVertex.x);
            minExtents.y = Math.Min(minExtents.y, rotatedVertex.z);
        }

        // Calculate final extents and center
        extents = (maxExtents - minExtents) * 0.5f;
        center = minExtents + extents;

        projectedRadius = Math.Sqrt(projectedArea / Math.PI);
    }

    public double GetConvectionMultVerts(OcclusionCone cone)
    {
        double occludedCount = 0.0;
        double shockCount = 0.0;
        double coneRadiusSq = cone.radius * cone.radius;

        for (int i = 0; i < 8; i++)
        {
            double vertexDot = projectedDots[i];
            double shockRadiusSq = Math.Pow(cone.GetShockRadius(vertexDot), 2);
            double distanceSq = (projectedVertices[i] - cone.center).sqrMagnitude;

            // Check if vertex is within cone radius
            if (distanceSq <= coneRadiusSq && vertexDot < cone.cylNoseDot)
            {
                occludedCount += 0.125;
                shockCount += 0.125;
            }
            // Check if vertex is within shock radius
            else if (distanceSq <= shockRadiusSq && vertexDot < cone.shockNoseDot)
            {
                shockCount += 0.125;
            }
        }

        return 1.0 - shockCount + 
               (shockCount - occludedCount) * cone.shockConvectionTempMult + 
               occludedCount * cone.occludeConvectionTempMult;
    }

    public double GetShockStats(OcclusionCone cone, ref double newTempMult, ref double newCoeffMult)
    {
        // Calculate intersection of bounding rectangles
        Vector2 minBound = cone.offset + minExtents;
        Vector2 maxBound = cone.offset + maxExtents;
        double intersectionRatio = RectRectIntersection(
            cone.extents.x, cone.extents.y, 
            minBound.x, maxBound.x, minBound.y, maxBound.y);

        // Handle NaN values
        if (double.IsNaN(intersectionRatio))
        {
            intersectionRatio = 0.0;
        }

        double shockOverlap = 1.0;
        if (intersectionRatio < 0.99)
        {
            double shockRadius = cone.GetShockRadius(centroidDot);
            shockOverlap = AreaOfIntersection(
                shockRadius, projectedRadius, 
                (projectedCenter - cone.center).sqrMagnitude);
        }
        else
        {
            intersectionRatio = 1.0;
        }

        // Calculate weighted multipliers
        double outsideRatio = 1.0 - shockOverlap;
        double transitionRatio = shockOverlap - intersectionRatio;
        
        newTempMult = outsideRatio * newTempMult + 
                     transitionRatio * cone.shockConvectionTempMult + 
                     intersectionRatio * cone.occludeConvectionTempMult;
                     
        newCoeffMult = outsideRatio * newCoeffMult + 
                      transitionRatio * cone.shockConvectionCoeffMult + 
                      intersectionRatio * cone.occludeConvectionCoeffMult;

        return 1.0 - intersectionRatio + intersectionRatio * cone.occludeConvectionAreaMult;
    }

    public double GetCylinderOcclusion(OcclusionCylinder cyl)
    {
        // Calculate rectangle intersection for cylinder occlusion
        Vector2 minBound = cyl.offset + minExtents;
        Vector2 maxBound = cyl.offset + maxExtents;
        double intersection = RectRectIntersection(
            cyl.extents.x, cyl.extents.y,
            minBound.x, maxBound.x, minBound.y, maxBound.y);

        if (double.IsNaN(intersection))
        {
            intersection = 0.0;
        }

        return intersection;
    }

    public double GetCylinderOcclusionVerts(OcclusionCylinder cyl)
    {
        double occludedCount = 0.0;
        double radiusSquared = cyl.radius * cyl.radius;

        for (int i = 0; i < 8; i++)
        {
            double vertexDot = projectedDots[i];
            double distanceSquared = (projectedVertices[i] - cyl.center).sqrMagnitude;

            if (distanceSquared <= radiusSquared && vertexDot < cyl.cylNoseDot)
            {
                occludedCount += 0.125;
            }
        }

        return occludedCount;
    }

    protected static double SignedArea(double r, double x, double y)
    {
        // Handle negative coordinates by symmetry
        if (x < 0.0)
            return -SignedArea(r, -x, y);
        if (y < 0.0)
            return -SignedArea(r, x, -y);

        // Clamp to circle bounds
        if (x > r) x = r;
        if (y > r) y = r;

        // Calculate intersection area
        if (x * x + y * y > r * r)
        {
            double area = r * r * Math.Asin(x / r) + x * Math.Sqrt(r * r - x * x) +
                         r * r * Math.Asin(y / r) + y * Math.Sqrt(r * r - y * y) -
                         r * r * Math.PI * 0.5;
            return area * 0.5;
        }
        
        return x * y;
    }

    protected static double RectRectIntersection(double centralExtentX, double centralExtentY, 
                                              double minX, double maxX, double minY, double maxY)
    {
        double totalArea = (maxX - minX) * (maxY - minY);

        // Check if rectangles overlap
        if (maxX >= -centralExtentX && minX <= centralExtentX &&
            maxY >= -centralExtentY && minY <= centralExtentY && totalArea != 0.0)
        {
            double overlapWidth = Math.Max(0.0, Math.Min(centralExtentX, maxX) - Math.Max(-centralExtentX, minX));
            double overlapHeight = Math.Max(0.0, Math.Min(centralExtentY, maxY) - Math.Max(-centralExtentY, minY));
            return (overlapWidth * overlapHeight) / totalArea;
        }

        return 0.0;
    }

    protected static double AreaOfIntersection(double coneRadius1, double coneRadius2, double distanceSquared)
    {
        double sumRadii = coneRadius1 + coneRadius2;
        
        // No intersection if too far apart
        if (distanceSquared >= sumRadii * sumRadii)
            return 0.0;

        // Handle zero radius cases
        if (coneRadius2 == 0.0)
            return 1.0;
        if (coneRadius1 == 0.0)
            return 0.0;

        double distance = Math.Sqrt(distanceSquared);

        // Complete containment cases
        if (coneRadius1 >= distance + coneRadius2)
            return 1.0;
        if (coneRadius2 >= distance + coneRadius1)
            return Mathf.Clamp01((float)(coneRadius1 * coneRadius1) / (float)(coneRadius2 * coneRadius2));

        // Calculate circular segment intersection
        double r1 = Math.Min(coneRadius1, coneRadius2);
        double r2 = Math.Max(coneRadius1, coneRadius2);
        double r1Sq = r1 * r1;
        double r2Sq = r2 * r2;

        double acos1 = Math.Acos((distanceSquared + r1Sq - r2Sq) / (2.0 * distance * r1));
        double acos2 = Math.Acos((distanceSquared + r2Sq - r1Sq) / (2.0 * distance * r2));
        double sqrtTerm = Math.Sqrt((-distance + sumRadii) * (distance + r1 - r2) * (distance - r1 + r2) * (distance + sumRadii));

        // Check for valid calculations
        if (!double.IsNaN(acos1) && !double.IsNaN(acos2) && !double.IsNaN(sqrtTerm))
        {
            double intersectionArea = r1Sq * acos1 + r2Sq * acos2 - sqrtTerm;
            return intersectionArea / (Math.PI * coneRadius2 * coneRadius2);
        }
        

        return 0.0;
    }

    public int CompareTo(OcclusionData other)
    {
        if (other.maximumDot < maximumDot)
            return 1;
        if (other.maximumDot == maximumDot)
            return 0;
        return -1;
    }
}

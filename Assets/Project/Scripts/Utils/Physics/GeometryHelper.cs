using System.Collections.Generic;
using UnityEngine;

public static class GeometryHelper
{
    public struct Wall
    {
        public Vector3 Center;
        public Vector3 Normal;
        public Bounds Bounds;
    }

    public static IEnumerable<Wall> GetVerticalWalls(Bounds bounds)
    {
        var c = bounds.center;
        var e = bounds.extents;

        yield return new Wall
        {
            Center = new Vector3(c.x + e.x, c.y, c.z),
            Normal = Vector3.right,
            Bounds = bounds
        };

        yield return new Wall
        {
            Center = new Vector3(c.x - e.x, c.y, c.z),
            Normal = Vector3.left,
            Bounds = bounds
        };

        yield return new Wall
        {
            Center = new Vector3(c.x, c.y, c.z + e.z),
            Normal = Vector3.forward,
            Bounds = bounds
        };

        yield return new Wall
        {
            Center = new Vector3(c.x, c.y, c.z - e.z),
            Normal = Vector3.back,
            Bounds = bounds
        };
    }

    public struct Edge
    {
        public Vector3 Point1;
        public Vector3 Point2;
        public Collider Building;
    }

    public static List<Edge> ExtractRooftopEdges(Collider[] buildings)
    {
        var edges = new List<Edge>();

        foreach (var building in buildings)
        {
            var b = building.bounds;
            float top = b.max.y;

            // Front edge
            edges.Add(new Edge
            {
                Point1 = new Vector3(b.min.x, top, b.min.z),
                Point2 = new Vector3(b.max.x, top, b.min.z),
                Building = building
            });

            // Right edge
            edges.Add(new Edge
            {
                Point1 = new Vector3(b.max.x, top, b.min.z),
                Point2 = new Vector3(b.max.x, top, b.max.z),
                Building = building
            });

            // Back edge
            edges.Add(new Edge
            {
                Point1 = new Vector3(b.max.x, top, b.max.z),
                Point2 = new Vector3(b.min.x, top, b.max.z),
                Building = building
            });

            // Left edge
            edges.Add(new Edge
            {
                Point1 = new Vector3(b.min.x, top, b.max.z),
                Point2 = new Vector3(b.min.x, top, b.min.z),
                Building = building
            });
        }

        return edges;
    }

    public static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segmentDir = segmentEnd - segmentStart;
        float segmentLength = segmentDir.magnitude;

        if (segmentLength < 0.0001f)
            return segmentStart;

        segmentDir /= segmentLength;

        Vector3 toPoint = point - segmentStart;
        float projection = Vector3.Dot(toPoint, segmentDir);
        float t = Mathf.Clamp01(projection / segmentLength);

        return Vector3.Lerp(segmentStart, segmentEnd, t);
    }

    public static Vector3 ClosestPointOnEdgeToLine(
        Vector3 lineStart,
        Vector3 lineEnd,
        Vector3 edgeStart,
        Vector3 edgeEnd)
    {
        var lineDir = (lineEnd - lineStart).normalized;
        var edgeDir = (edgeEnd - edgeStart).normalized;
        var toEdge = edgeStart - lineStart;

        float proj = Vector3.Dot(toEdge, lineDir);
        var closestOnLine = lineStart + proj * lineDir;

        var toClosest = closestOnLine - edgeStart;
        float edgeLen = Vector3.Distance(edgeStart, edgeEnd);
        float t = Mathf.Clamp01(Vector3.Dot(toClosest, edgeDir) / edgeLen);

        return Vector3.Lerp(edgeStart, edgeEnd, t);
    }

    public static bool IsPointOnWall(Vector3 point, Wall wall, float margin = 0.1f)
    {
        var b = wall.Bounds;

        // Check distance to plane
        float distToPlane = Mathf.Abs(Vector3.Dot(point - wall.Center, wall.Normal));
        if (distToPlane > margin)
            return false;

        // Check if within bounds for each wall orientation
        if (Mathf.Abs(wall.Normal.x) > 0.9f)
            return point.y >= b.min.y - margin && point.y <= b.max.y + margin &&
                   point.z >= b.min.z - margin && point.z <= b.max.z + margin;

        if (Mathf.Abs(wall.Normal.z) > 0.9f)
            return point.y >= b.min.y - margin && point.y <= b.max.y + margin &&
                   point.x >= b.min.x - margin && point.x <= b.max.x + margin;

        return false;
    }
}


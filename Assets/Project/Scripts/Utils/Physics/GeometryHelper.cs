using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Propagation.Core;

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
        public bool IsCorner;
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
                Building = building,
                IsCorner = false
            });

            // Right edge
            edges.Add(new Edge
            {
                Point1 = new Vector3(b.max.x, top, b.min.z),
                Point2 = new Vector3(b.max.x, top, b.max.z),
                Building = building,
                IsCorner = false
            });

            // Back edge
            edges.Add(new Edge
            {
                Point1 = new Vector3(b.max.x, top, b.max.z),
                Point2 = new Vector3(b.min.x, top, b.max.z),
                Building = building,
                IsCorner = false
            });

            // Left edge
            edges.Add(new Edge
            {
                Point1 = new Vector3(b.min.x, top, b.max.z),
                Point2 = new Vector3(b.min.x, top, b.min.z),
                Building = building,
                IsCorner = false
            });
        }

        return edges;
    }

    public static List<Edge> ExtractCornerEdges(Collider[] buildings)
    {
        var edges = new List<Edge>();
        foreach (var building in buildings)
        {
            if (building == null) continue;

            var b = building.bounds;
            float baseY = b.min.y;
            float topY = b.max.y;

            // Bottom corner positions
            var c1 = new Vector3(b.min.x, baseY, b.min.z); 
            var c2 = new Vector3(b.max.x, baseY, b.min.z); 
            var c3 = new Vector3(b.max.x, baseY, b.max.z);
            var c4 = new Vector3(b.min.x, baseY, b.max.z); 

            // Top corner positions
            var c1Top = new Vector3(c1.x, topY, c1.z);
            var c2Top = new Vector3(c2.x, topY, c2.z);
            var c3Top = new Vector3(c3.x, topY, c3.z);
            var c4Top = new Vector3(c4.x, topY, c4.z);

            // Vertical edges at corners
            edges.Add(new Edge { Point1 = c1, Point2 = c1Top, Building = building, IsCorner = true });
            edges.Add(new Edge { Point1 = c2, Point2 = c2Top, Building = building, IsCorner = true });
            edges.Add(new Edge { Point1 = c3, Point2 = c3Top, Building = building, IsCorner = true });
            edges.Add(new Edge { Point1 = c4, Point2 = c4Top, Building = building, IsCorner = true });
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

    //public static bool IsPointOnWall(Vector3 point, Wall wall, float margin = 0.1f)
    //{
    //    var b = wall.Bounds;

    //    // Check distance to plane
    //    float distToPlane = Mathf.Abs(Vector3.Dot(point - wall.Center, wall.Normal));
    //    if (distToPlane > margin)
    //        return false;

    //    // Check if within bounds for each wall orientation
    //    if (Mathf.Abs(wall.Normal.x) > 0.9f)
    //        return point.y >= b.min.y - margin && point.y <= b.max.y + margin &&
    //               point.z >= b.min.z - margin && point.z <= b.max.z + margin;

    //    if (Mathf.Abs(wall.Normal.z) > 0.9f)
    //        return point.y >= b.min.y - margin && point.y <= b.max.y + margin &&
    //               point.x >= b.min.x - margin && point.x <= b.max.x + margin;

    //    return false;
    //}

    public static bool IsPointOnWall(Vector3 p, Wall wall)
    {
        var b = wall.Bounds;
        float margin = 0.1f;

        if (Mathf.Abs(wall.Normal.x) > 0.9f)
            return p.y >= b.min.y - margin && p.y <= b.max.y + margin &&
                   p.z >= b.min.z - margin && p.z <= b.max.z + margin;

        if (Mathf.Abs(wall.Normal.z) > 0.9f)
            return p.y >= b.min.y - margin && p.y <= b.max.y + margin &&
                   p.x >= b.min.x - margin && p.x <= b.max.x + margin;

        return false;
    }


    // Check if edge obstructs TX-RX line
    public static bool EdgeObstructs(Vector3 tx, Vector3 rx, Vector3 edge)
    {
        var dir = (rx - tx).normalized;
        var toEdge = edge - tx;
        float alongPath = Vector3.Dot(toEdge, dir);
        float dist = Vector3.Distance(tx, rx);

        if (alongPath < 0 || alongPath > dist) return false;

        var closestOnLine = tx + alongPath * dir;
        float perpDist = Vector3.Distance(edge, closestOnLine);

        float t = alongPath / dist;
        float lineHeight = Mathf.Lerp(tx.y, rx.y, t);

        return edge.y > lineHeight && perpDist > 0.1f;
    }

    // Get reflection point using image method
    public static bool GetReflectionPoint(Vector3 tx, Vector3 rx, GeometryHelper.Wall wall, out Vector3 point)
    {
        float d = Vector3.Dot(rx - wall.Center, wall.Normal);
        var rxImage = rx - 2f * d * wall.Normal;

        var v = rxImage - tx;
        float denom = Vector3.Dot(wall.Normal, v);
        if (Mathf.Abs(denom) < RFConstants.EPS)
        {
            point = Vector3.zero;
            return false;
        }

        float t = Vector3.Dot(wall.Normal, wall.Center - tx) / denom;
        if (t <= 0f || t >= 1f)
        {
            point = Vector3.zero;
            return false;
        }

        point = tx + t * v;

        return GeometryHelper.IsPointOnWall(point, wall);
    }

}


using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.Serialization;

public class NPCMovementLine : MonoBehaviour
{
    public Transform[] locations;
    [Range(0, 1)] public float debugInterpolatedValue = 0f;
    public Transform debugArtificialPoint;
    [HideInInspector] public Vector3[] points;


    
/*
    public Vector3 GetNormal(Rigidbody rigidbody)
    {
       return GetQuickInterpolatedNormal(rigidbody.position, _line);
    }

    public void RotateTransformToMatchNormal(ref Transform transform)
    {
        Quaternion rotation = Quaternion.LookRotation(EdgeNormal, transform.up);
        Quaternion adjustedRotation = rotation * Quaternion.Euler(0, 90, 0);
        transform.rotation = adjustedRotation;
    }
    
    public void RotateRigidbodyToMatchNormal(ref Rigidbody rigidbody)
    {
        Transform transform = rigidbody.transform;
        Quaternion rotation = Quaternion.LookRotation(EdgeNormal, transform.up);
        Quaternion adjustedRotation = rotation * Quaternion.Euler(0, 90, 0);
        rigidbody.MoveRotation(adjustedRotation);
        //Debug.Log("Rotation is "+adjustedRotation.eulerAngles);
    }

    public void GetSetNearestPointOnLine(Vector3 point)
    {
        _rawPoint = point;
        GetInterpolatedValue(_rawPoint, _line, ref _edgePoints, ref EdgeNormal, ref Point, ref _nearestPoint);
    }
    
    public Vector3 GetNearestPointOnLine(Vector3 point)
    {
        return GetQuickInterpolatedValue(point, _line);
    }

    public Vector3 InterpolatedPosition(float t)
    {
        return Interpolate(t, _line);
    }
    
    public float ReverseInterpolatedPosition(Vector3 t)
    {
        return ReverseInterpolate(t, _line);
        }*/

    public Vector3[] GetLineSegmentsThatMatchNormal(Vector3 normal)
    {
        for(int i = 0; i < points.Length - 1; i ++)
        {
            var pointNormal = (points[i + 1] - points[i]).normalized;
            if (Mathf.Approximately(pointNormal.x, normal.x))
            {
                if (Mathf.Approximately(pointNormal.y, normal.y))
                {
                    if (Mathf.Approximately(pointNormal.z, normal.z))
                    {
                        return new[] { points[i], points[i + 1] };
                    }
                }
            }
        }
        Debug.LogError("No normals matching!!");
        return new[] { points[0], points[1] };
    }

    private static float ReverseInterpolate(Vector3 point, Vector3[] line)
    {
        float totalLength = 0;
        for (int i = 1; i < line.Length; i++)
        {
            totalLength += Vector3.Distance(line[i - 1], line[i]);
        }

        float accumulatedLength = 0;

        for (int i = 1; i < line.Length; i++)
        {
            float segmentLength = Vector3.Distance(line[i - 1], line[i]);
            Vector3 segmentDirection = (line[i] - line[i - 1]).normalized;
            Vector3 pointDirection = (point - line[i - 1]).normalized;

            if (Vector3.Dot(segmentDirection, pointDirection) > 0.999f) // Check if the point is on the segment
            {
                float pointSegmentLength = Vector3.Distance(line[i - 1], point);
                float segmentT = pointSegmentLength / segmentLength;
                float t = (accumulatedLength + pointSegmentLength) / totalLength;
                return t;
            }

            accumulatedLength += segmentLength;
        }

        return 1.0f; // If the point is at the end of the line
    }

    public float GetLineLength()
    {
        float totalLength = 0;
        
        for (int i = 1; i < points.Length; i++)
        {
            totalLength += Vector3.Distance(points[i - 1], points[i]);
        }

        return totalLength;
    }

    // Take a point in line-space 0-1, and a direction +/- in world units, and return the new interpolated value of that
    // world-space offset
    // Used for patrolling - we don't want to follow a world-space target for this, as its harder to know where  to
    // place it, than just using raw line-space
    public float TrasformOffsetWorldSpaceToLineSpace(float worldSpaceUnits)
    {
        float lineLegth = GetLineLength();
        float increment = 1f / lineLegth;
        float offset = worldSpaceUnits * increment;
        return offset;
    }
    
    public Vector3 Interpolate(float t)
    {
        float totalLength = GetLineLength();
        
        float targetLength = t * totalLength;
        float accumulatedLength = 0;

        for (int i = 1; i < points.Length; i++)
        {
            float segmentLength = Vector3.Distance(points[i - 1], points[i]);
            if (accumulatedLength + segmentLength >= targetLength)
            {
                float segmentT = (targetLength - accumulatedLength) / segmentLength;
                return Vector3.Lerp(points[i - 1], points[i], segmentT);
            }
            accumulatedLength += segmentLength;
        }
        return points[^1];
    }


    private static Vector3 GetClosestPointOnFiniteLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDirection = lineEnd - lineStart;
        float lineLength = lineDirection.magnitude;
        lineDirection.Normalize();
        float projectLength = Mathf.Clamp(Vector3.Dot(point - lineStart, lineDirection), 0f, lineLength);
        return lineStart + lineDirection * projectLength;
    }

    private static Vector3 GetPoint(Vector3 p, Vector3 a, Vector3 b)
    {
        return a + Vector3.Project(p - a, b - a);
    }

    private void Awake()
    {
        if (locations.Length < 2) return;
        
        points = new Vector3[locations.Length + 1];
        for(int j = 0; j < locations.Length; j++)
        {
            points[j] = locations[j].position;
        }

        points[locations.Length] = locations[0].position;
    }

    public float GetInterpolatedPointFromAnyPosition(Vector3 point)
    {
        point = GetClosestPointOnLine(point);
        var interpolatedPoint = ReverseInterpolate(point, points);
        return interpolatedPoint;
    }
    
    public float GetInterpolatedPointFromPosition(Vector3 point)
    {
        var interpolatedPoint = ReverseInterpolate(point, points);
        return interpolatedPoint;
    }
    
    public Vector3 GetClosestPointOnLine(Vector3 point)
    {
        var nearestPoints = GetLineSegment(point);
        var closestPointOnLine = GetClosestPointOnFiniteLine(point, nearestPoints[0], nearestPoints[1]);
        return closestPointOnLine;
    }
    
    public Vector3[] GetLineSegmentFromAnyPosition(Vector3 point)
    {
        point = GetClosestPointOnLine(point);
        return GetLineSegment(point);
    }

    Vector3[] GetLineSegment(Vector3 point)
    {
        float closestT = float.MaxValue;
        int closestIndex = 0;
        
        
        for (int i = 0; i < points.Length; i++)
        {
            float distance = Vector3.Distance(points[i], point);
            if (distance < closestT)
            {
                closestT = distance;
                closestIndex = i;
            }
        }

        int nextIndex = closestIndex >= points.Length - 1 ? 0 : closestIndex + 1;
        int prevIndex = closestIndex <= 0 ? points.Length - 2 : closestIndex - 1;

        Vector3 nextIndexProjected = GetClosestPointOnFiniteLine(point, points[closestIndex], points[nextIndex] );
        Vector3 prevIndexProjected = GetClosestPointOnFiniteLine(point, points[prevIndex], points[closestIndex] );
        
        float nextIndexDistance = Vector3.Distance(nextIndexProjected, point);
        float prevIndexDistance = Vector3.Distance(prevIndexProjected, point);

        Vector3 index = nextIndexDistance < prevIndexDistance ? points[nextIndex] : points[prevIndex];

        var retVector = new Vector3[2];
        retVector[0] = index;
        retVector[1] = points[closestIndex];
        return retVector;
    }

    public Vector3 GetEdgeNormalFromLinePosition(Vector3 point)
    {
        var lineSegment = GetLineSegment(point);
        var normal = (lineSegment[1] - lineSegment[0]).normalized;
        return normal;
    }

    public Vector3 GetEdgeNormalFromAnyPosition(Vector3 point)
    {
        var lineSegment = GetLineSegmentFromAnyPosition(point);
        var normal = (lineSegment[1] - lineSegment[0]).normalized;
        return normal;
    }

    
    public void RotateRigidbodyToMatchNormal(Rigidbody rb, Vector3 normal)
    {
        Transform tf = rb.transform;
        var projectedPosition = GetClosestPointOnLine(rb.position);
        Debug.DrawRay(projectedPosition, normal * 5, Color.cyan,20f);
        //Debug.Log(normal);
        Quaternion rotation = Quaternion.LookRotation(normal, tf.up);
        Quaternion adjustedRotation = rotation * Quaternion.Euler(0, 90, 0);
        rb.MoveRotation(adjustedRotation);
        //Debug.Log("Rotation is "+adjustedRotation.eulerAngles);
    }
    
    
    public void DBG_GetInterpolatedValue(Vector3 point)
    {
        float closestT = float.MaxValue;
        int closestIndex = 0;

        for (int i = 0; i < points.Length; i++)
        {
            float distance = Vector3.Distance(points[i], point);
            if (distance < closestT)
            {
                closestT = distance;
                closestIndex = i;
            }
        }

        int nextIndex = closestIndex >= points.Length - 1 ? 0 : closestIndex + 1;
        int prevIndex = closestIndex <= 0 ? points.Length - 2 : closestIndex - 1;



        Vector3 nextIndexProjected = GetClosestPointOnFiniteLine(point, points[closestIndex], points[nextIndex] );
        Vector3 prevIndexProjected = GetClosestPointOnFiniteLine(point, points[prevIndex], points[closestIndex] );
        
        
        float nextIndexDistance = Vector3.Distance(nextIndexProjected, point);
        float prevIndexDistance = Vector3.Distance(prevIndexProjected, point);
        Gizmos.color = Color.magenta;

        Gizmos.DrawLine(point, points[nextIndex]);
        Gizmos.color = Color.cyan;

        Gizmos.DrawLine(point, points[prevIndex]);
        Vector3 index = nextIndexDistance < prevIndexDistance ? nextIndexProjected : prevIndexProjected;
        if (nextIndexDistance < prevIndexDistance)
        {
            Gizmos.color = Color.white;

            Gizmos.DrawCube(points[nextIndex], Vector3.one * 3f);

        }
        else
        {
            Gizmos.color = Color.black;

            Gizmos.DrawCube(points[prevIndex], Vector3.one * 3f);

        }
        
        Vector3 nearestPoint = points[closestIndex];
        Gizmos.color = Color.yellow;
        Gizmos.DrawCube(nearestPoint, Vector3.one * 3f);
        
        
        Gizmos.color = Color.green;

        Gizmos.DrawCube(GetPoint(point,points[closestIndex], index ), Vector3.one * 1f);
        
        Gizmos.color = Color.grey;
        Gizmos.DrawSphere(Interpolate(debugInterpolatedValue), 1f);
        
        

    }
    
    private void OnDrawGizmos()
    {

        if (locations.Length < 2) return;
        
        points = new Vector3[locations.Length + 1];
        for(int j = 0; j < locations.Length; j++)
        {
            points[j] = locations[j].position;
        }

        points[locations.Length] = locations[0].position;
        
        Gizmos.color = Color.blue;
        if (points != null && points.Length > 1)
        {
            for(int i = 0; i < points.Length -1; i++)
            {
                Gizmos.DrawLine(points[i], points[i +1]);
            }
        }
        Gizmos.color = Color.cyan;
        for(int i = 0; i < points.Length; i++)
        {
            Gizmos.DrawCube(points[i], Vector3.one * 0.3f);
        }

        if (debugArtificialPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(debugArtificialPoint.position, Vector3.one * 0.3f);
            if (points != null && points.Length > 1)
            {
                //Vector3 pos = Interpolate(0.5f * (Mathf.Sin(Time.time) + 1));
                //Vector3 pos = Interpolate(amount);
                DBG_GetInterpolatedValue(debugArtificialPoint.position);
                Vector3 pos = Interpolate(debugInterpolatedValue);
                Gizmos.color = Color.red;
                Gizmos.DrawCube(pos, Vector3.one * 2f);
            }
        }
        
    }

}

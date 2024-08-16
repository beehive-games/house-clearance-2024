using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager.UI;
using UnityEngine;

public class NPCMovementLine : MonoBehaviour
{
    public Transform[] locations;
    [Range(0, 1)] public float amount = 0f;
    public Transform artificialPoint;
    private Vector3[] points;

    public SampledLine GetLineData(Vector3 point)
    {
        if (locations.Length < 2)
        {
            Debug.LogError("Line doesnt exist!");
        }

        if (points.Length < 1)
        {
            points = new Vector3[locations.Length + 1];
            for(int j = 0; j < locations.Length; j++)
            {
                points[j] = locations[j].position;
            }

            points[locations.Length] = locations[0].position;
        }

        
        
        return new SampledLine(points, point);
    }

    public struct SampledLine
    {
        private Vector3 _point; // point on line
        private Vector3 _rawPoint; // point on line
        private readonly Vector3[] _line;
        private Vector3[] _edgePoints;
        private Vector3 _edgeNormal;
        private Vector3 _nearestPoint;
        public float Increment;

        public void GetNearestPointOnLine(Vector3 point)
        {
            _rawPoint = _point;
            GetInterpolatedValue(_rawPoint, _line, ref _edgePoints, ref _edgeNormal, ref this._point, ref _nearestPoint);
        }

        public Vector3 InterpolatedPosition(float t)
        {
            return Interpolate(t, _line);
        }

        public SampledLine(Vector3[] line, Vector3 point)
        {
            _line = line;
            _rawPoint = point;
            
            _point = new Vector3();
            _edgePoints = new Vector3[2];
            _edgeNormal = Vector3.right;
            _nearestPoint = _point;
            
            float totalDistance = 0f;
            for (int i = 1; i < line.Length; i++)
            {
                totalDistance += Vector3.Distance(line[i-1], line[i ]);
            }
            Increment = 1f / totalDistance;
            
            GetInterpolatedValue(_rawPoint, line, ref _edgePoints, ref _edgeNormal, ref this._point, ref _nearestPoint);
        }
    }
    
    static Vector3 Interpolate(float t, Vector3[] line)
    {
        float totalLength = 0;
        for (int i = 1; i < line.Length; i++)
        {
            totalLength += Vector3.Distance(line[i - 1], line[i]);
        }
        
        float targetLength = t * totalLength;
        float accumulatedLength = 0;

        for (int i = 1; i < line.Length; i++)
        {
            float segmentLength = Vector3.Distance(line[i - 1], line[i]);
            if (accumulatedLength + segmentLength >= targetLength)
            {
                float segmentT = (targetLength - accumulatedLength) / segmentLength;
                return Vector3.Lerp(line[i - 1], line[i], segmentT);
            }
            accumulatedLength += segmentLength;
        }
        return line[^1];
    }
    
    
    static Vector3 GetClosestPointOnFiniteLine(Vector3 point, Vector3 line_start, Vector3 line_end)
    {
        Vector3 line_direction = line_end - line_start;
        float line_length = line_direction.magnitude;
        line_direction.Normalize();
        float project_length = Mathf.Clamp(Vector3.Dot(point - line_start, line_direction), 0f, line_length);
        return line_start + line_direction * project_length;
    }
    
    static Vector3 GetPoint(Vector3 p, Vector3 a, Vector3 b)
    {
        return a + Vector3.Project(p - a, b - a);
    }


    private static void GetInterpolatedValue(Vector3 point, Vector3[] line, ref Vector3[] edges, ref Vector3 normal, ref Vector3 sampledPoint, ref Vector3 nearestPoint )
    {
        float closestT = float.MaxValue;
        int closestIndex = 0;
        
        
        for (int i = 0; i < line.Length; i++)
        {
            float distance = Vector3.Distance(line[i], point);
            if (distance < closestT)
            {
                closestT = distance;
                closestIndex = i;
            }
        }

        int nextIndex = closestIndex >= line.Length - 1 ? 0 : closestIndex + 1;
        int prevIndex = closestIndex <= 0 ? line.Length - 2 : closestIndex - 1;

        Vector3 nextIndexProjected = GetClosestPointOnFiniteLine(point, line[closestIndex], line[nextIndex] );
        Vector3 prevIndexProjected = GetClosestPointOnFiniteLine(point, line[prevIndex], line[closestIndex] );
        
        
        float nextIndexDistance = Vector3.Distance(nextIndexProjected, point);
        float prevIndexDistance = Vector3.Distance(prevIndexProjected, point);

        Vector3 index = nextIndexDistance < prevIndexDistance ? nextIndexProjected : prevIndexProjected;

        sampledPoint = GetPoint(point, line[closestIndex], index);
        nearestPoint = line[closestIndex];
        edges[0] = line[closestIndex];
        edges[1] = index;
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
        Gizmos.DrawSphere(Interpolate(amount, points), 1f);
        
        

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

        if (artificialPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(artificialPoint.position, Vector3.one * 0.3f);
            if (points != null && points.Length > 1)
            {
                //Vector3 pos = Interpolate(0.5f * (Mathf.Sin(Time.time) + 1));
                //Vector3 pos = Interpolate(amount);
                DBG_GetInterpolatedValue(artificialPoint.position);
                Vector3 pos = Interpolate(amount, points);
                Gizmos.color = Color.red;
                Gizmos.DrawCube(pos, Vector3.one * 2f);
            }
        }
        
    }

}

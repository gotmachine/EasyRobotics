using System.Reflection;
using UnityEngine;

public static class DrawTools
{
    private static Material _material;

    private static int glDepth = 0;

    private static Material material
    {
        get
        {
            if (_material == null) _material = new Material(Shader.Find("Hidden/Internal-Colored"));
            return _material;
        }
    }

    // Ok that's cheap but I did not want to add a bunch 
    // of try catch to make sure the GL calls ends.
    public static void NewFrame()
    {
        if (glDepth > 0)
            MonoBehaviour.print(glDepth);
        glDepth = 0;
    }

    private static void GLStart()
    {
        if (glDepth == 0)
        {
            GL.PushMatrix();
            material.SetPass(0);
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);
        }
        glDepth++;
    }

    private static void GLEnd()
    {
        glDepth--;

        if (glDepth == 0)
        {
            GL.End();
            GL.PopMatrix();
        }
    }


    private static Camera GetActiveCam()
    {
        Camera cam;
        if (HighLogic.LoadedSceneIsEditor)
            cam = EditorLogic.fetch.editorCamera;
        else if (HighLogic.LoadedSceneIsFlight)
            cam = MapView.MapIsEnabled ? PlanetariumCamera.Camera : FlightCamera.fetch.mainCamera;
        else
            cam = Camera.main;
        return cam;
    }

    private static Vector3 Tangent(Vector3 normal)
    {
        Vector3 tangent = Vector3.Cross(normal, Vector3.right);
        if (tangent.sqrMagnitude <= float.Epsilon)
            tangent = Vector3.Cross(normal, Vector3.up);
        return tangent;
    }

    private static void DrawLineImpl(Vector3 origin, Vector3 destination, Color color)
    {
        Camera cam = GetActiveCam();

        Vector3 screenPoint1 = cam.WorldToScreenPoint(origin);
        Vector3 screenPoint2 = cam.WorldToScreenPoint(destination);

        GL.Color(color);
        GL.Vertex3(screenPoint1.x, screenPoint1.y, 0);
        GL.Vertex3(screenPoint2.x, screenPoint2.y, 0);
    }

    private static void DrawRayImpl(Vector3 origin, Vector3 direction, Color color)
    {
        Camera cam = GetActiveCam();

        Vector3 screenPoint1 = cam.WorldToScreenPoint(origin);
        Vector3 screenPoint2 = cam.WorldToScreenPoint(origin + direction);

        GL.Color(color);
        GL.Vertex3(screenPoint1.x, screenPoint1.y, 0);
        GL.Vertex3(screenPoint2.x, screenPoint2.y, 0);
    }

    public static void DrawLine(Vector3 origin, Vector3 destination, Color color)
    {
        GLStart();
        DrawLineImpl(origin, destination, color);
        GLEnd();
    }


    public static void DrawTransform(Transform t, float scale = 1.0f)
    {
        GLStart();

        DrawRayImpl(t.position, t.up * scale, Color.green);
        DrawRayImpl(t.position, t.right * scale, Color.red);
        DrawRayImpl(t.position, t.forward * scale, Color.blue);

        GLEnd();
    }
    public static void DrawPoint(Vector3 position, Color color, float scale = 1.0f)
    {
        GLStart();
        GL.Color(color);

        DrawRayImpl(position + Vector3.up * (scale * 0.5f), -Vector3.up * scale, color);
        DrawRayImpl(position + Vector3.right * (scale * 0.5f), -Vector3.right * scale, color);
        DrawRayImpl(position + Vector3.forward * (scale * 0.5f), -Vector3.forward * scale, color);

        GLEnd();
    }

    public static void DrawArrow(Vector3 position, Vector3 direction, Color color)
    {
        GLStart();
        GL.Color(color);

        DrawRayImpl(position, direction, color);

        GLEnd();

        DrawCone(position + direction, -direction * 0.333f, color, 15);
    }

    public static void DrawCone(Vector3 position, Vector3 direction, Color color, float angle = 45)
    {
        float length = direction.magnitude;

        Vector3 forward = direction;
        Vector3 up = Tangent(forward).normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;

        float radius = length * Mathf.Tan(Mathf.Deg2Rad * angle);

        GLStart();
        GL.Color(color);

        DrawRayImpl(position, direction + radius * up, color);
        DrawRayImpl(position, direction - radius * up, color);
        DrawRayImpl(position, direction + radius * right, color);
        DrawRayImpl(position, direction - radius * right, color);

        GLEnd();

        DrawCircle(position + forward, direction, color, radius);
        DrawCircle(position + forward * 0.5f, direction, color, radius * 0.5f);
    }

    public static void DrawLocalMesh(Transform transform, Mesh mesh, Color color)
    {
        if (mesh == null || mesh.triangles == null || mesh.vertices == null)
            return;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        GLStart();
        GL.Color(color);

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 p1 = transform.TransformPoint(vertices[triangles[i]]);
            Vector3 p2 = transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 p3 = transform.TransformPoint(vertices[triangles[i + 2]]);
            DrawLineImpl(p1, p2, color);
            DrawLineImpl(p2, p3, color);
            DrawLineImpl(p3, p1, color);
        }

        GLEnd();
    }

    public static void DrawBounds(Bounds bounds, Color color)
    {
        Vector3 center = bounds.center;

        float x = bounds.extents.x;
        float y = bounds.extents.y;
        float z = bounds.extents.z;

        Vector3 topa = center + new Vector3(x, y, z);
        Vector3 topb = center + new Vector3(x, y, -z);
        Vector3 topc = center + new Vector3(-x, y, z);
        Vector3 topd = center + new Vector3(-x, y, -z);

        Vector3 bota = center + new Vector3(x, -y, z);
        Vector3 botb = center + new Vector3(x, -y, -z);
        Vector3 botc = center + new Vector3(-x, -y, z);
        Vector3 botd = center + new Vector3(-x, -y, -z);

        GLStart();
        GL.Color(color);

        // Top
        DrawLineImpl(topa, topc, color);
        DrawLineImpl(topa, topb, color);
        DrawLineImpl(topc, topd, color);
        DrawLineImpl(topb, topd, color);

        // Sides
        DrawLineImpl(topa, bota, color);
        DrawLineImpl(topb, botb, color);
        DrawLineImpl(topc, botc, color);
        DrawLineImpl(topd, botd, color);

        // Bottom
        DrawLineImpl(bota, botc, color);
        DrawLineImpl(bota, botb, color);
        DrawLineImpl(botc, botd, color);
        DrawLineImpl(botd, botb, color);

        GLEnd();
    }

    public static void DrawRectTransform(RectTransform rectTransform, Canvas canvas, Color color)
    {

        Vector3[] corners = new Vector3[4];
        Vector3[] screenCorners = new Vector3[2];

        rectTransform.GetWorldCorners(corners);

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
        {
            screenCorners[0] = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[1]);
            screenCorners[1] = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[3]);
        }
        else
        {
            screenCorners[0] = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
            screenCorners[1] = RectTransformUtility.WorldToScreenPoint(null, corners[3]);
        }

        GLStart();
        GL.Color(color);

        GL.Vertex3(screenCorners[0].x, screenCorners[0].y, 0);
        GL.Vertex3(screenCorners[0].x, screenCorners[1].y, 0);

        GL.Vertex3(screenCorners[0].x, screenCorners[1].y, 0);
        GL.Vertex3(screenCorners[1].x, screenCorners[1].y, 0);

        GL.Vertex3(screenCorners[1].x, screenCorners[1].y, 0);
        GL.Vertex3(screenCorners[1].x, screenCorners[0].y, 0);

        GL.Vertex3(screenCorners[1].x, screenCorners[0].y, 0);
        GL.Vertex3(screenCorners[0].x, screenCorners[0].y, 0);

        GLEnd();
    }


    public static void DrawLocalCube(Transform transform, Vector3 size, Color color, Vector3 center = default(Vector3))
    {
        Vector3 topa = transform.TransformPoint(center + new Vector3(-size.x, size.y, -size.z) * 0.5f);
        Vector3 topb = transform.TransformPoint(center + new Vector3(size.x, size.y, -size.z) * 0.5f);

        Vector3 topc = transform.TransformPoint(center + new Vector3(size.x, size.y, size.z) * 0.5f);
        Vector3 topd = transform.TransformPoint(center + new Vector3(-size.x, size.y, size.z) * 0.5f);

        Vector3 bota = transform.TransformPoint(center + new Vector3(-size.x, -size.y, -size.z) * 0.5f);
        Vector3 botb = transform.TransformPoint(center + new Vector3(size.x, -size.y, -size.z) * 0.5f);

        Vector3 botc = transform.TransformPoint(center + new Vector3(size.x, -size.y, size.z) * 0.5f);
        Vector3 botd = transform.TransformPoint(center + new Vector3(-size.x, -size.y, size.z) * 0.5f);

        GLStart();
        GL.Color(color);

        //top
        DrawLineImpl(topa, topb, color);
        DrawLineImpl(topb, topc, color);
        DrawLineImpl(topc, topd, color);
        DrawLineImpl(topd, topa, color);

        //Sides
        DrawLineImpl(topa, bota, color);
        DrawLineImpl(topb, botb, color);
        DrawLineImpl(topc, botc, color);
        DrawLineImpl(topd, botd, color);

        //Bottom
        DrawLineImpl(bota, botb, color);
        DrawLineImpl(botb, botc, color);
        DrawLineImpl(botc, botd, color);
        DrawLineImpl(botd, bota, color);

        GLEnd();
    }

    public static void DrawCapsule(Vector3 start, Vector3 end, Color color, float radius = 1)
    {
        int segments = 18;
        float segmentsInv = 1f / segments;

        Vector3 up = (end - start).normalized * radius;
        Vector3 forward = Tangent(up).normalized * radius;
        Vector3 right = Vector3.Cross(up, forward).normalized * radius;

        float height = (start - end).magnitude;
        float sideLength = Mathf.Max(0, height * 0.5f - radius);
        Vector3 middle = (end + start) * 0.5f;

        start = middle + (start - middle).normalized * sideLength;
        end = middle + (end - middle).normalized * sideLength;

        //Radial circles
        DrawCircle(start, up, color, radius);
        DrawCircle(end, -up, color, radius);

        GLStart();
        GL.Color(color);

        //Side lines
        DrawLineImpl(start + right, end + right, color);
        DrawLineImpl(start - right, end - right, color);

        DrawLineImpl(start + forward, end + forward, color);
        DrawLineImpl(start - forward, end - forward, color);

        for (int i = 1; i <= segments; i++)
        {
            float stepFwd = i * segmentsInv;
            float stepBck = (i - 1) * segmentsInv;
            //Start endcap
            DrawLineImpl(Vector3.Slerp(right, -up, stepFwd) + start, Vector3.Slerp(right, -up, stepBck) + start, color);
            DrawLineImpl(Vector3.Slerp(-right, -up, stepFwd) + start, Vector3.Slerp(-right, -up, stepBck) + start, color);
            DrawLineImpl(Vector3.Slerp(forward, -up, stepFwd) + start, Vector3.Slerp(forward, -up, stepBck) + start, color);
            DrawLineImpl(Vector3.Slerp(-forward, -up, stepFwd) + start, Vector3.Slerp(-forward, -up, stepBck) + start, color);

            //End endcap
            DrawLineImpl(Vector3.Slerp(right, up, stepFwd) + end, Vector3.Slerp(right, up, stepBck) + end, color);
            DrawLineImpl(Vector3.Slerp(-right, up, stepFwd) + end, Vector3.Slerp(-right, up, stepBck) + end, color);
            DrawLineImpl(Vector3.Slerp(forward, up, stepFwd) + end, Vector3.Slerp(forward, up, stepBck) + end, color);
            DrawLineImpl(Vector3.Slerp(-forward, up, stepFwd) + end, Vector3.Slerp(-forward, up, stepBck) + end, color);
        }

        GLEnd();
    }

    public static void DrawCircle(Vector3 position, Vector3 up, Color color, float radius = 1.0f)
    {
        int segments = 36;
        float step = Mathf.Deg2Rad * 360f / segments;

        Vector3 upNormal = up.normalized * radius;
        Vector3 forwardNormal = Tangent(upNormal).normalized * radius;
        Vector3 rightNormal = Vector3.Cross(upNormal, forwardNormal).normalized * radius;

        Matrix4x4 matrix = new Matrix4x4();

        matrix[0] = rightNormal.x;
        matrix[1] = rightNormal.y;
        matrix[2] = rightNormal.z;

        matrix[4] = upNormal.x;
        matrix[5] = upNormal.y;
        matrix[6] = upNormal.z;

        matrix[8] = forwardNormal.x;
        matrix[9] = forwardNormal.y;
        matrix[10] = forwardNormal.z;

        Vector3 lastPoint = position + matrix.MultiplyPoint3x4(Vector3.right);

        GLStart();
        GL.Color(color);

        for (int i = 0; i <= segments; i++)
        {
            Vector3 nextPoint;
            var angle = i * step;
            nextPoint.x = Mathf.Cos(angle);
            nextPoint.z = Mathf.Sin(angle);
            nextPoint.y = 0;

            nextPoint = position + matrix.MultiplyPoint3x4(nextPoint);

            DrawLineImpl(lastPoint, nextPoint, color);
            lastPoint = nextPoint;
        }
        GLEnd();
    }

    public static void DrawSphere(Vector3 position, Color color, float radius = 1.0f)
    {
        int segments = 36;
        float step = Mathf.Deg2Rad * 360f / segments;

        Vector3 x = new Vector3(position.x, position.y, position.z + radius);
        Vector3 y = new Vector3(position.x + radius, position.y, position.z);
        Vector3 z = new Vector3(position.x + radius, position.y, position.z);

        GLStart();
        GL.Color(color);

        for (int i = 1; i <= segments; i++)
        {
            float angle = step * i;
            Vector3 nextX = new Vector3(position.x, position.y + radius * Mathf.Sin(angle), position.z + radius * Mathf.Cos(angle));
            Vector3 nextY = new Vector3(position.x + radius * Mathf.Cos(angle), position.y, position.z + radius * Mathf.Sin(angle));
            Vector3 nextZ = new Vector3(position.x + radius * Mathf.Cos(angle), position.y + radius * Mathf.Sin(angle), position.z);

            DrawLineImpl(x, nextX, color);
            DrawLineImpl(y, nextY, color);
            DrawLineImpl(z, nextZ, color);

            x = nextX;
            y = nextY;
            z = nextZ;
        }
        GLEnd();
    }

    public static void DrawCylinder(Vector3 start, Vector3 end, Color color, float radius = 1)
    {
        Vector3 up = (end - start).normalized * radius;
        Vector3 forward = Tangent(up);
        Vector3 right = Vector3.Cross(up, forward).normalized * radius;

        //Radial circles
        DrawCircle(start, up, color, radius);
        DrawCircle(end, -up, color, radius);
        DrawCircle((start + end) * 0.5f, up, color, radius);

        GLStart();
        GL.Color(color);

        //Sides
        DrawLineImpl(start + right, end + right, color);
        DrawLineImpl(start - right, end - right, color);

        DrawLineImpl(start + forward, end + forward, color);
        DrawLineImpl(start - forward, end - forward, color);

        //Top
        DrawLineImpl(start - right, start + right, color);
        DrawLineImpl(start - forward, start + forward, color);

        //Bottom
        DrawLineImpl(end - right, end + right, color);
        DrawLineImpl(end - forward, end + forward, color);
        GLEnd();
    }

    private static FieldInfo jointMode;

    public static void DrawJoint(PartJoint joint)
    {
        if (joint == null)
            return;

        if (joint.Host == null || joint.Child == null || joint.Parent == null)
            return;

        Color col = joint.Host == joint.Child ? Color.blue : Color.red;

        if (jointMode == null)
            jointMode = typeof(PartJoint).GetField("mode", BindingFlags.NonPublic | BindingFlags.Instance);

        float node = ((AttachModes)jointMode.GetValue(joint)) == AttachModes.STACK ? 1 : 0.6f;

        GLStart();
        GL.Color(col * node);

        DrawLineImpl(joint.Child.transform.position, joint.Parent.transform.position, col * node);

        GLEnd();
    }
}
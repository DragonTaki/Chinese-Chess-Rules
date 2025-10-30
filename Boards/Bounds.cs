/* ----- ----- ----- ----- */
// Bounds.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/14
// Update Date: 2025/05/14
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Drawing;

using Engine.Mathematics;

namespace Chinese_Chess_v3.Game.Core.Boards
{
    public class Bounds
    {
        public Vector2F Position { get; set; }
        public Vector2F Size { get; set; }

        public float X => Position.X;
        public float Y => Position.Y;
        public float Width => Size.X;
        public float Height => Size.Y;

        public RectangleF Rect => new RectangleF(X, Y, Width, Height);

        public static Bounds Empty => new Bounds(new Vector2F(0, 0), new Vector2F(0, 0));

        public Bounds(Vector2F position, Vector2F size)
        {
            Position = position;
            Size = size;
        }

        public bool Contains(Vector2F point)
        {
            return point.X >= X && point.X <= X + Width &&
                   point.Y >= Y && point.Y <= Y + Height;
        }
    }
}
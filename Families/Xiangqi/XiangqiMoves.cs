/* ----- ----- ----- ----- */
// XiangqiMoves.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Movements;
using Chinese_Chess_v3.Game.Core.Pieces;

namespace Chinese_Chess_v3.Game.Core.Families.Xiangqi
{
    /// <summary>
    /// How each piece moves on the Full board (standard xiangqi and 揭棋): palaces, the river,
    /// the Elephant's eye, the Horse's leg, the Cannon's screen, facing Generals (王見王) and 吃己棋.
    /// These are the pieces' own movement rules only ("pseudo-legal"); whether a move leaves the
    /// mover's General attacked is checked on top (see <see cref="XiangqiFamily"/>).
    /// <para>
    /// 揭棋 (<see cref="Board.IsJieqi"/>): a piece still face down moves as whatever piece
    /// canonically starts at its square (<see cref="PieceConstants.GetClassicPieceTypeAt"/>), not
    /// as itself; once revealed it moves as itself, and a revealed Advisor / Elephant may leave
    /// the palace / cross the river.
    /// </para>
    /// </summary>
    internal static class XiangqiMoves
    {
        /// <summary>Whether <paramref name="piece"/> may move to (targetX, targetY) by its own movement rules.</summary>
        public static bool IsPseudoLegalMove(Board board, Piece piece, int targetX, int targetY) =>
            MovingType(board, piece) switch
            {
                PieceType.General => IsValidGeneral(board, piece, targetX, targetY),
                PieceType.Advisor => IsValidStep(board, piece, targetX, targetY, MovePatterns.GetDiagonalOneStep(piece.Color), PieceType.Advisor),
                PieceType.Elephant => IsValidElephant(board, piece, targetX, targetY),
                PieceType.Horse => IsValidHorse(board, piece, targetX, targetY),
                PieceType.Chariot => IsValidChariot(board, piece, targetX, targetY),
                PieceType.Cannon => IsValidCannon(board, piece, targetX, targetY),
                PieceType.Soldier => IsValidStep(board, piece, targetX, targetY,
                    MovePatterns.GetSoldierDirections(piece.Color, HasCrossedRiver(piece.Color, piece.Y)), PieceType.Soldier),
                _ => throw new ArgumentException($"No Full-board movement for {piece.Type}", nameof(piece)),
            };

        /// <summary>Every square <paramref name="piece"/> may move to by its own movement rules.</summary>
        public static List<(int x, int y)> GetPseudoLegalMoves(Board board, Piece piece) =>
            MovingType(board, piece) switch
            {
                PieceType.General => GetGeneralMoves(board, piece),
                PieceType.Advisor => GetStepMoves(board, piece, MovePatterns.GetDiagonalOneStep(piece.Color), PieceType.Advisor),
                PieceType.Elephant => GetElephantMoves(board, piece),
                PieceType.Horse => GetHorseMoves(board, piece),
                PieceType.Chariot => GetChariotMoves(board, piece),
                PieceType.Cannon => GetCannonMoves(board, piece),
                PieceType.Soldier => GetStepMoves(board, piece,
                    MovePatterns.GetSoldierDirections(piece.Color, HasCrossedRiver(piece.Color, piece.Y)), PieceType.Soldier),
                _ => throw new ArgumentException($"No Full-board movement for {piece.Type}", nameof(piece)),
            };

        /// <summary>
        /// The type <paramref name="piece"/> moves as: its own, or on a 揭棋 board while face down the
        /// type that starts on its square. A face-down piece only stands off its starting square
        /// during a move's simulation (a real move turns it face up at once), and then it already
        /// counts as revealed: its own type.
        /// </summary>
        public static PieceType MovingType(Board board, Piece piece)
        {
            if (!board.IsJieqi || piece.CurrentInfo.IsFaceUp)
                return piece.Type;
            var startType = PieceConstants.GetClassicPieceTypeAt(piece.X, piece.Y);
            return startType == PieceType.None ? piece.Type : startType;
        }

        #region Shared checks

        /// <summary>
        /// Whether a piece moving as <paramref name="movingType"/> may stand on (targetX, targetY):
        /// on the board, and the General / Advisor in their palace, the Elephant on its own half
        /// (unless a rule or 揭棋 frees them).
        /// </summary>
        private static bool IsDestinationLegal(Board board, Piece piece, PieceType movingType, int targetX, int targetY)
        {
            switch (movingType)
            {
                case PieceType.General:
                    return board.GameRules.CanGeneralLeavePalace
                        ? board.IsInBoard(targetX, targetY)
                        : board.IsInPalace(piece.Color, targetX, targetY);

                case PieceType.Advisor:
                    // 揭棋: once revealed, an Advisor can leave the palace freely. A still-hidden
                    // Advisor making its first move is bound by the normal palace restriction.
                    if (board.IsJieqi && piece.CurrentInfo.IsFaceUp)
                        return board.IsInBoard(targetX, targetY);
                    return board.GameRules.CanAdvisorLeavePalace
                        ? board.IsInBoard(targetX, targetY)
                        : board.IsInPalace(piece.Color, targetX, targetY);

                case PieceType.Elephant:
                    // 揭棋: once revealed, an Elephant can cross the river freely.
                    if (board.IsJieqi && piece.CurrentInfo.IsFaceUp)
                        return board.IsInBoard(targetX, targetY);
                    if (!board.IsInBoard(targetX, targetY))
                        return false;
                    // The own half is keyed by colour, not player (Player1 can play Black).
                    return piece.Color switch
                    {
                        PieceColor.Black => targetY <= BoardConstants.Full.RiverLineYBlackSide,
                        PieceColor.Red => targetY >= BoardConstants.Full.RiverLineYRedSide,
                        // Only the Full board's two colours have a river side.
                        _ => throw new InvalidOperationException($"{piece.Color} has no side of the river on the Full board"),
                    };

                default:
                    return board.IsInBoard(targetX, targetY);
            }
        }

        /// <summary>
        /// Whether the piece on (targetX, targetY) is one <paramref name="piece"/> may not move onto
        /// because it is its own (吃己棋). An own piece always blocks unless
        /// <see cref="Rules.CanCaptureOwnPiece"/> is on, and the own General always blocks (the
        /// Full board's check rules need both Generals; author decision 2026-10-02).
        /// </summary>
        private static bool IsBlockedByOwnPiece(Board board, Piece piece, int targetX, int targetY)
        {
            var target = board.GetPiece(targetX, targetY);
            if (target == null || target.Side != piece.Side)
                return false;
            // Its own square is never a destination.
            if (target == piece)
                return true;
            return !board.GameRules.CanCaptureOwnPiece || target.Type == PieceType.General;
        }

        /// <summary>
        /// Flying-General check (王見王) for a non-General piece moving from its square to
        /// (targetX, targetY): true if the move would leave the Generals facing each other and
        /// <see cref="Rules.CanGeneralSeeGeneral"/> is off.
        /// </summary>
        private static bool WouldExposeGenerals(Board board, Piece piece, int targetX, int targetY) =>
            !board.GameRules.CanGeneralSeeGeneral && board.IsGeneralFaceToFaceAfterMove(piece.X, piece.Y, targetX, targetY);

        private static bool Matches((int dx, int dy)[] directions, int dx, int dy)
        {
            foreach (var (dirX, dirY) in directions)
            {
                if (dx == dirX && dy == dirY)
                    return true;
            }
            return false;
        }

        /// <summary>Whether a Soldier of <paramref name="color"/> on row <paramref name="y"/> has crossed the river (keyed by colour, not player).</summary>
        private static bool HasCrossedRiver(PieceColor color, int y) => color switch
        {
            // Black's own half is Y 0-4, Red's is Y 5-9; a soldier has crossed once it stands on
            // the far half - the same test as Board.IsPassRiver.
            PieceColor.Black => y > BoardConstants.Full.RiverLineYBlackSide,
            PieceColor.Red => y < BoardConstants.Full.RiverLineYRedSide,
            _ => throw new InvalidOperationException($"{color} has no side of the river on the Full board"),
        };

        #endregion

        #region One-step pieces (Advisor, Soldier)

        private static bool IsValidStep(Board board, Piece piece, int targetX, int targetY, (int dx, int dy)[] directions, PieceType movingType)
        {
            if (!IsDestinationLegal(board, piece, movingType, targetX, targetY))
                return false;
            if (WouldExposeGenerals(board, piece, targetX, targetY))
                return false;
            if (!Matches(directions, targetX - piece.X, targetY - piece.Y))
                return false;
            return !IsBlockedByOwnPiece(board, piece, targetX, targetY);
        }

        private static List<(int x, int y)> GetStepMoves(Board board, Piece piece, (int dx, int dy)[] directions, PieceType movingType)
        {
            var legalMoves = new List<(int x, int y)>();
            foreach (var (dx, dy) in directions)
            {
                int newX = piece.X + dx;
                int newY = piece.Y + dy;
                if (!IsDestinationLegal(board, piece, movingType, newX, newY))
                    continue;
                if (WouldExposeGenerals(board, piece, newX, newY))
                    continue;
                if (IsBlockedByOwnPiece(board, piece, newX, newY))
                    continue;
                legalMoves.Add((newX, newY));
            }
            return legalMoves;
        }

        #endregion

        #region General

        private static bool IsValidGeneral(Board board, Piece piece, int targetX, int targetY)
        {
            if (!IsDestinationLegal(board, piece, PieceType.General, targetX, targetY))
                return false;
            // The General's own move may not face the other General.
            if (!board.GameRules.CanGeneralSeeGeneral && !board.IsGeneralTargetLegal(piece.Side, targetX, targetY))
                return false;
            if (!Matches(MovePatterns.GetOrthogonalOneStep(piece.Color), targetX - piece.X, targetY - piece.Y))
                return false;
            return !IsBlockedByOwnPiece(board, piece, targetX, targetY);
        }

        private static List<(int x, int y)> GetGeneralMoves(Board board, Piece piece)
        {
            var legalMoves = new List<(int x, int y)>();
            foreach (var (dx, dy) in MovePatterns.GetOrthogonalOneStep(piece.Color))
            {
                int newX = piece.X + dx;
                int newY = piece.Y + dy;
                if (!IsDestinationLegal(board, piece, PieceType.General, newX, newY))
                    continue;
                if (IsBlockedByOwnPiece(board, piece, newX, newY))
                    continue;
                if (!board.GameRules.CanGeneralSeeGeneral && !board.IsGeneralTargetLegal(piece.Side, newX, newY))
                    continue;
                legalMoves.Add((newX, newY));
            }
            return legalMoves;
        }

        #endregion

        #region Elephant

        private static bool IsValidElephant(Board board, Piece piece, int targetX, int targetY)
        {
            if (!IsDestinationLegal(board, piece, PieceType.Elephant, targetX, targetY))
                return false;
            if (WouldExposeGenerals(board, piece, targetX, targetY))
                return false;
            int dx = targetX - piece.X;
            int dy = targetY - piece.Y;
            if (!Matches(MovePatterns.GetDiagonalTwoStep(piece.Color), dx, dy))
                return false;
            if (board.GameRules.CanElephantEyeBlocked && IsElephantEyeBlocked(board, piece, dx, dy))
                return false;
            return !IsBlockedByOwnPiece(board, piece, targetX, targetY);
        }

        private static List<(int x, int y)> GetElephantMoves(Board board, Piece piece)
        {
            var legalMoves = new List<(int x, int y)>();
            foreach (var (dx, dy) in MovePatterns.GetDiagonalTwoStep(piece.Color))
            {
                int newX = piece.X + dx;
                int newY = piece.Y + dy;
                if (!IsDestinationLegal(board, piece, PieceType.Elephant, newX, newY))
                    continue;
                if (WouldExposeGenerals(board, piece, newX, newY))
                    continue;
                if (board.GameRules.CanElephantEyeBlocked && IsElephantEyeBlocked(board, piece, dx, dy))
                    continue;
                if (IsBlockedByOwnPiece(board, piece, newX, newY))
                    continue;
                legalMoves.Add((newX, newY));
            }
            return legalMoves;
        }

        /// <summary>Whether the square between the Elephant and its target (the "eye") holds a piece.</summary>
        private static bool IsElephantEyeBlocked(Board board, Piece piece, int dx, int dy)
        {
            if (Math.Abs(dx) != 2 || Math.Abs(dy) != 2)
                throw new ArgumentException($"Not an Elephant move offset: ({dx},{dy})");
            return board.Grid[piece.X + dx / 2, piece.Y + dy / 2] != null;
        }

        #endregion

        #region Horse

        private static bool IsValidHorse(Board board, Piece piece, int targetX, int targetY)
        {
            if (!IsDestinationLegal(board, piece, PieceType.Horse, targetX, targetY))
                return false;
            if (WouldExposeGenerals(board, piece, targetX, targetY))
                return false;
            int dx = targetX - piece.X;
            int dy = targetY - piece.Y;
            if (!Matches(MovePatterns.GetDiagonalLShape(piece.Color), dx, dy))
                return false;
            if (board.GameRules.CanHorseLegHobbled && IsHorseLegHobbled(board, piece, dx, dy))
                return false;
            return !IsBlockedByOwnPiece(board, piece, targetX, targetY);
        }

        private static List<(int x, int y)> GetHorseMoves(Board board, Piece piece)
        {
            var legalMoves = new List<(int x, int y)>();
            foreach (var (dx, dy) in MovePatterns.GetDiagonalLShape(piece.Color))
            {
                int newX = piece.X + dx;
                int newY = piece.Y + dy;
                if (!IsDestinationLegal(board, piece, PieceType.Horse, newX, newY))
                    continue;
                if (WouldExposeGenerals(board, piece, newX, newY))
                    continue;
                if (board.GameRules.CanHorseLegHobbled && IsHorseLegHobbled(board, piece, dx, dy))
                    continue;
                if (IsBlockedByOwnPiece(board, piece, newX, newY))
                    continue;
                legalMoves.Add((newX, newY));
            }
            return legalMoves;
        }

        /// <summary>Whether the square next to the Horse in the long direction of its move (the "leg") holds a piece.</summary>
        private static bool IsHorseLegHobbled(Board board, Piece piece, int dx, int dy)
        {
            int blockX = piece.X;
            int blockY = piece.Y;
            if (Math.Abs(dx) == 2)
                blockX += Math.Sign(dx);
            if (Math.Abs(dy) == 2)
                blockY += Math.Sign(dy);
            return board.Grid[blockX, blockY] != null;
        }

        #endregion

        #region Chariot

        private static bool IsValidChariot(Board board, Piece piece, int targetX, int targetY)
        {
            if (!IsDestinationLegal(board, piece, PieceType.Chariot, targetX, targetY))
                return false;
            if (WouldExposeGenerals(board, piece, targetX, targetY))
                return false;
            int dx = targetX - piece.X;
            int dy = targetY - piece.Y;
            if (dx != 0 && dy != 0)
                return false;
            if (MoveLines.CountPiecesBetween(board, piece.X, piece.Y, targetX, targetY) != 0)
                return false;
            return !IsBlockedByOwnPiece(board, piece, targetX, targetY);
        }

        private static List<(int x, int y)> GetChariotMoves(Board board, Piece piece)
        {
            var legalMoves = new List<(int x, int y)>();
            foreach (var (dx, dy) in MovePatterns.GetOrthogonalOneStep(piece.Color))
            {
                int newX = piece.X + dx;
                int newY = piece.Y + dy;
                while (board.IsInBoard(newX, newY))
                {
                    var obstacle = board.Grid[newX, newY];
                    // Checked per square: moving along the Generals' column is still legal while leaving it is not.
                    bool exposes = WouldExposeGenerals(board, piece, newX, newY);
                    if (obstacle == null)
                    {
                        if (!exposes)
                            legalMoves.Add((newX, newY));
                    }
                    else
                    {
                        if (!IsBlockedByOwnPiece(board, piece, newX, newY) && !exposes)
                            legalMoves.Add((newX, newY));
                        break;
                    }
                    newX += dx;
                    newY += dy;
                }
            }
            return legalMoves;
        }

        #endregion

        #region Cannon

        private static bool IsValidCannon(Board board, Piece piece, int targetX, int targetY)
        {
            if (!IsDestinationLegal(board, piece, PieceType.Cannon, targetX, targetY))
                return false;
            if (WouldExposeGenerals(board, piece, targetX, targetY))
                return false;
            int dx = targetX - piece.X;
            int dy = targetY - piece.Y;
            if (dx != 0 && dy != 0)
                return false;
            int count = MoveLines.CountPiecesBetween(board, piece.X, piece.Y, targetX, targetY);
            // A quiet move needs a clear line; a capture exactly one screen (and not an own piece).
            if (board.Grid[targetX, targetY] == null)
                return count == 0;
            return count == 1 && !IsBlockedByOwnPiece(board, piece, targetX, targetY);
        }

        private static List<(int x, int y)> GetCannonMoves(Board board, Piece piece)
        {
            var legalMoves = new List<(int x, int y)>();
            foreach (var (dx, dy) in MovePatterns.GetOrthogonalOneStep(piece.Color))
            {
                bool jumped = false;
                int newX = piece.X + dx;
                int newY = piece.Y + dy;
                while (board.IsInBoard(newX, newY))
                {
                    var target = board.Grid[newX, newY];
                    if (!jumped)
                    {
                        if (target == null)
                        {
                            if (!WouldExposeGenerals(board, piece, newX, newY))
                                legalMoves.Add((newX, newY));
                        }
                        else
                        {
                            // The first piece met is the screen to jump over.
                            jumped = true;
                        }
                    }
                    else if (target != null)
                    {
                        // The next piece behind the screen is the only capture target.
                        if (!IsBlockedByOwnPiece(board, piece, newX, newY) && !WouldExposeGenerals(board, piece, newX, newY))
                            legalMoves.Add((newX, newY));
                        break;
                    }
                    newX += dx;
                    newY += dy;
                }
            }
            return legalMoves;
        }

        #endregion
    }
}

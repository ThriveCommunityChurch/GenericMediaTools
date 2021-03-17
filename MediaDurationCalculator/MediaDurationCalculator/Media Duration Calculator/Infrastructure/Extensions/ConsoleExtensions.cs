using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace MediaDurationCalculator.Infrastructure.Extensions
{
    [ExcludeFromCodeCoverage]
    internal static class ConsoleExtensions
    {
        internal static void PrintWithColor(
            string message,
            ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        internal static void PrintInfo(
            string message,
            ConsoleColor backGroundColor = default,
            ConsoleColor foreGroundColor = ConsoleColor.White,
            Type declaringType = null)
        {
            var backGround = GetCurrentConsoleColorIfDefault(backGroundColor);

            PrintWithBackground(
                message,
                backGround,
                foreGroundColor,
                declaringType);
        }

        internal static void PrintWarning(
            string message,
            ConsoleColor backGroundColor = default,
            ConsoleColor foreGroundColor = ConsoleColor.DarkYellow,
            Type declaringType = null)
        {
            var backGround = GetCurrentConsoleColorIfDefault(backGroundColor);

            PrintWithBackground(
                message,
                backGround,
                foreGroundColor,
                declaringType);
        }

        internal static void PrintError(
            string message,
            ConsoleColor backGroundColor = default,
            ConsoleColor foreGroundColor = ConsoleColor.DarkRed,
            Type declaringType = null)
        {
            var backGround = GetCurrentConsoleColorIfDefault(backGroundColor);

            PrintWithBackground(
                message,
                backGround,
                foreGroundColor,
                declaringType);
        }

        internal static void WriteSuccess(
            string message,
            ConsoleColor backGroundColor = default,
            ConsoleColor foreGroundColor = ConsoleColor.Green,
            Type declaringType = null)
        {
            var backGround = GetCurrentConsoleColorIfDefault(backGroundColor);

            PrintWithBackground(
                message,
                backGround,
                foreGroundColor,
                declaringType);
        }

        internal static void PrintWithBackground(
            string message,
            ConsoleColor backGroundColor,
            ConsoleColor foreGroundColor,
            Type declaringType = null)
        {
            var currentBackground = Console.BackgroundColor;
            var currentText = Console.ForegroundColor;

            Console.BackgroundColor = backGroundColor;
            Console.ForegroundColor = foreGroundColor;

            var consoleMessage = $"{message}";

            if (!string.IsNullOrWhiteSpace(declaringType?.Name))
            {
                consoleMessage = $"{declaringType?.Name} - {message}";
            }

            Console.WriteLine(consoleMessage);

            Console.BackgroundColor = currentBackground;
            Console.ForegroundColor = currentText;
        }

        internal static void PrintOnSingleLineAfterDelay(
            string message,
            int delay)
        {
            Task.WaitAll(Task.Delay(delay));
            Console.Write(message);
        }

        internal static void PrintStartMessage(
            string operation,
            ConsoleColor messageColor = ConsoleColor.Magenta)
        {
            PrintWithColor(
                $"Initializing {operation}...\n",
                messageColor);
        }

        internal static void PrintExitMessage(
            string operation,
            int exitCode,
            Stopwatch watch,
            ConsoleColor successColor = ConsoleColor.DarkGreen,
            ConsoleColor failureColor = ConsoleColor.DarkRed)
        {
            var elapsedMinutes = watch.Elapsed.Minutes;
            var elapsedSeconds = watch.Elapsed.Seconds;

            if (exitCode != -1)
            {
                PrintWithColor(
                    $"\n{operation} Completed In: {elapsedMinutes}m {elapsedSeconds}s.",
                    successColor);
            }

            if (exitCode == -1)
            {
                PrintWithColor(
                    $"\n{operation} Failed After: {elapsedMinutes}m {elapsedSeconds}m.",
                    failureColor);
            }

#if DEBUG
            Console.WriteLine("Press Key");
            if (!Console.IsErrorRedirected && !Console.IsOutputRedirected)
            {
                Console.ReadKey();
            }
#endif
        }

        private static ConsoleColor GetCurrentConsoleColorIfDefault(
            ConsoleColor input,
            bool isBackground = false)
        {
            var currentConsoleColor = isBackground ? Console.BackgroundColor : Console.ForegroundColor;
            return input == default ? currentConsoleColor : input;
        }
    }
}
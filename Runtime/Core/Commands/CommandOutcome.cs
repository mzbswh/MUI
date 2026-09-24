using System;

namespace MUI
{
    public enum CommandStatus
    {
        Rejected,
        Succeeded,
        Cancelled,
        Failed
    }

    public enum CommandRejection
    {
        None,
        Unavailable,
        Reentrant
    }

    public readonly struct CommandOutcome
    {
        private CommandOutcome(CommandStatus status, Exception error, CommandRejection rejection = CommandRejection.None)
        {
            Status = status;
            Error = error;
            Rejection = rejection;
        }

        public CommandStatus Status
        {
            get;
        }

        public Exception Error
        {
            get;
        }

        public CommandRejection Rejection
        {
            get;
        }

        public bool IsSuccess => Status == CommandStatus.Succeeded;

        public static CommandOutcome Succeeded() => new CommandOutcome(CommandStatus.Succeeded, null);

        public static CommandOutcome Rejected() => new CommandOutcome(CommandStatus.Rejected, null, CommandRejection.Unavailable);

        internal static CommandOutcome Reentrant() => new CommandOutcome(CommandStatus.Rejected, null, CommandRejection.Reentrant);

        public static CommandOutcome Cancelled() => new CommandOutcome(CommandStatus.Cancelled, null);

        public static CommandOutcome Failed(Exception error) => new CommandOutcome(CommandStatus.Failed, error ?? throw new ArgumentNullException(nameof(error)));
    }
}

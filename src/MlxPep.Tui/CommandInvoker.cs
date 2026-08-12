using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MlxPep.Cli;
using MlxPep.Cli.Commands;

namespace MlxPep.Tui
{
    /// <summary>
    /// Invokes CLI command handlers in-process with output capture.
    /// This component wraps CLI handlers for programmatic access from the TUI.
    /// </summary>
    internal class CommandInvoker
    {
        private readonly StringWriter _outputWriter = new();
        private readonly StringWriter _errorWriter = new();

        /// <summary>
        /// Gets the captured standard output from the last command invocation.
        /// </summary>
        public string Output => _outputWriter.ToString();

        /// <summary>
        /// Gets the captured error output from the last command invocation.
        /// </summary>
        public string ErrorOutput => _errorWriter.ToString();

        /// <summary>
        /// Resets the output and error buffers before running a new command.
        /// </summary>
        public void ClearOutput()
        {
            _outputWriter.GetStringBuilder().Clear();
            _errorWriter.GetStringBuilder().Clear();
        }

        /// <summary>
        /// Invokes the Doctor command handler.
        /// </summary>
        public async Task<CommandResult> InvokeDoctorAsync()
        {
            Debug.WriteLine("CommandInvoker.InvokeDoctorAsync called");
            ClearOutput();
            
            try
            {
                var command = new DoctorCommand();
                
                // TODO: Create proper command context and invoke handler
                // For now, this is a stub that shows the structure
                _outputWriter.WriteLine("Doctor command invocation stub");
                
                return CommandResult.Success("Doctor command completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CommandInvoker.InvokeDoctorAsync failed: {ex.Message}");
                _errorWriter.WriteLine($"Error: {ex.Message}");
                return CommandResult.Error($"Doctor command failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Invokes the Models list command handler.
        /// </summary>
        public async Task<CommandResult> InvokeModelsListAsync()
        {
            Debug.WriteLine("CommandInvoker.InvokeModelsListAsync called");
            ClearOutput();
            
            try
            {
                var command = new ModelsListCommand();
                
                // TODO: Create proper command context and invoke handler
                _outputWriter.WriteLine("Models list command invocation stub");
                
                return CommandResult.Success("Models list command completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CommandInvoker.InvokeModelsListAsync failed: {ex.Message}");
                _errorWriter.WriteLine($"Error: {ex.Message}");
                return CommandResult.Error($"Models list command failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Invokes the Models get command handler with a model ID.
        /// </summary>
        public async Task<CommandResult> InvokeModelsGetAsync(string modelId)
        {
            Debug.WriteLine($"CommandInvoker.InvokeModelsGetAsync called with modelId={modelId}");
            ClearOutput();
            
            try
            {
                var command = new ModelsGetCommand();
                
                // TODO: Create proper command context and invoke handler
                _outputWriter.WriteLine($"Models get command invocation stub for {modelId}");
                
                return CommandResult.Success("Models get command completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CommandInvoker.InvokeModelsGetAsync failed: {ex.Message}");
                _errorWriter.WriteLine($"Error: {ex.Message}");
                return CommandResult.Error($"Models get command failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Invokes the Profiles list command handler.
        /// </summary>
        public async Task<CommandResult> InvokeProfilesListAsync()
        {
            Debug.WriteLine("CommandInvoker.InvokeProfilesListAsync called");
            ClearOutput();
            
            try
            {
                var command = new ProfilesListCommand();
                
                // TODO: Create proper command context and invoke handler
                _outputWriter.WriteLine("Profiles list command invocation stub");
                
                return CommandResult.Success("Profiles list command completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CommandInvoker.InvokeProfilesListAsync failed: {ex.Message}");
                _errorWriter.WriteLine($"Error: {ex.Message}");
                return CommandResult.Error($"Profiles list command failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Invokes the Profiles search command handler.
        /// </summary>
        public async Task<CommandResult> InvokeProfilesSearchAsync(string query)
        {
            Debug.WriteLine($"CommandInvoker.InvokeProfilesSearchAsync called with query={query}");
            ClearOutput();
            
            try
            {
                var command = new ProfilesSearchCommand();
                
                // TODO: Create proper command context and invoke handler
                _outputWriter.WriteLine($"Profiles search command invocation stub for query: {query}");
                
                return CommandResult.Success("Profiles search command completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CommandInvoker.InvokeProfilesSearchAsync failed: {ex.Message}");
                _errorWriter.WriteLine($"Error: {ex.Message}");
                return CommandResult.Error($"Profiles search command failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Invokes the Profiles pull command handler.
        /// </summary>
        public async Task<CommandResult> InvokeProfilesPullAsync(string profileId)
        {
            Debug.WriteLine($"CommandInvoker.InvokeProfilesPullAsync called with profileId={profileId}");
            ClearOutput();
            
            try
            {
                var command = new ProfilesPullCommand();
                
                // TODO: Create proper command context and invoke handler
                _outputWriter.WriteLine($"Profiles pull command invocation stub for {profileId}");
                
                return CommandResult.Success("Profiles pull command completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CommandInvoker.InvokeProfilesPullAsync failed: {ex.Message}");
                _errorWriter.WriteLine($"Error: {ex.Message}");
                return CommandResult.Error($"Profiles pull command failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Invokes the Apply command handler.
        /// </summary>
        public async Task<CommandResult> InvokeApplyAsync(string profilePath, string harness)
        {
            Debug.WriteLine($"CommandInvoker.InvokeApplyAsync called with profilePath={profilePath}, harness={harness}");
            ClearOutput();
            
            try
            {
                var command = new ApplyCommand();
                
                // TODO: Create proper command context and invoke handler
                _outputWriter.WriteLine($"Apply command invocation stub for {profilePath} with harness {harness}");
                
                return CommandResult.Success("Apply command completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CommandInvoker.InvokeApplyAsync failed: {ex.Message}");
                _errorWriter.WriteLine($"Error: {ex.Message}");
                return CommandResult.Error($"Apply command failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Invokes the Assess command handler.
        /// </summary>
        public async Task<CommandResult> InvokeAssessAsync(string hfId, bool publish)
        {
            Debug.WriteLine($"CommandInvoker.InvokeAssessAsync called with hfId={hfId}, publish={publish}");
            ClearOutput();
            
            try
            {
                var command = new AssessCommand();
                
                // TODO: Create proper command context and invoke handler
                _outputWriter.WriteLine($"Assess command invocation stub for {hfId} with publish={publish}");
                
                return CommandResult.Success("Assess command completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CommandInvoker.InvokeAssessAsync failed: {ex.Message}");
                _errorWriter.WriteLine($"Error: {ex.Message}");
                return CommandResult.Error($"Assess command failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Result from a command invocation.
    /// </summary>
    internal class CommandResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }

        private CommandResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public static CommandResult Success(string message) => new(true, message);
        public static CommandResult Error(string message) => new(false, message);
    }
}

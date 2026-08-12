using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MlxPep.Tui
{
    /// <summary>
    /// Builds the menu structure and wires menu items to command handlers.
    /// This component maps TUI menu selections to CLI command invocations.
    /// </summary>
    internal class MenuBuilder
    {
        private readonly CommandInvoker _commandInvoker;
        private readonly ResultPane _resultPane;

        public MenuBuilder(CommandInvoker commandInvoker, ResultPane resultPane)
        {
            _commandInvoker = commandInvoker ?? throw new ArgumentNullException(nameof(commandInvoker));
            _resultPane = resultPane ?? throw new ArgumentNullException(nameof(resultPane));
        }

        /// <summary>
        /// Gets the menu structure for Doctor command.
        /// </summary>
        public MenuItemDefinition GetDoctorMenu()
        {
            Debug.WriteLine("MenuBuilder.GetDoctorMenu");
            
            return new MenuItemDefinition
            {
                Title = "Doctor",
                Description = "Run system diagnostics",
                Handler = async () =>
                {
                    var result = await _commandInvoker.InvokeDoctorAsync();
                    _resultPane.DisplayCommandResult(result, _commandInvoker.Output, _commandInvoker.ErrorOutput);
                }
            };
        }

        /// <summary>
        /// Gets the menu structure for Models command.
        /// </summary>
        public List<MenuItemDefinition> GetModelsMenu()
        {
            Debug.WriteLine("MenuBuilder.GetModelsMenu");
            
            return new List<MenuItemDefinition>
            {
                new MenuItemDefinition
                {
                    Title = "List",
                    Description = "List available models",
                    Handler = async () =>
                    {
                        var result = await _commandInvoker.InvokeModelsListAsync();
                        _resultPane.DisplayCommandResult(result, _commandInvoker.Output, _commandInvoker.ErrorOutput);
                    }
                },
                new MenuItemDefinition
                {
                    Title = "Get",
                    Description = "Get model details",
                    Handler = async () =>
                    {
                        // TODO: Get model ID from user input
                        var modelId = "gpt2";  // Placeholder
                        var result = await _commandInvoker.InvokeModelsGetAsync(modelId);
                        _resultPane.DisplayCommandResult(result, _commandInvoker.Output, _commandInvoker.ErrorOutput);
                    }
                }
            };
        }

        /// <summary>
        /// Gets the menu structure for Profiles command.
        /// </summary>
        public List<MenuItemDefinition> GetProfilesMenu()
        {
            Debug.WriteLine("MenuBuilder.GetProfilesMenu");
            
            return new List<MenuItemDefinition>
            {
                new MenuItemDefinition
                {
                    Title = "List",
                    Description = "List profiles",
                    Handler = async () =>
                    {
                        var result = await _commandInvoker.InvokeProfilesListAsync();
                        _resultPane.DisplayCommandResult(result, _commandInvoker.Output, _commandInvoker.ErrorOutput);
                    }
                },
                new MenuItemDefinition
                {
                    Title = "Search",
                    Description = "Search profiles",
                    Handler = async () =>
                    {
                        // TODO: Get search query from user input
                        var query = "";  // Placeholder
                        var result = await _commandInvoker.InvokeProfilesSearchAsync(query);
                        _resultPane.DisplayCommandResult(result, _commandInvoker.Output, _commandInvoker.ErrorOutput);
                    }
                },
                new MenuItemDefinition
                {
                    Title = "Pull",
                    Description = "Pull profile",
                    Handler = async () =>
                    {
                        // TODO: Get profile ID from user input
                        var profileId = "";  // Placeholder
                        var result = await _commandInvoker.InvokeProfilesPullAsync(profileId);
                        _resultPane.DisplayCommandResult(result, _commandInvoker.Output, _commandInvoker.ErrorOutput);
                    }
                }
            };
        }

        /// <summary>
        /// Gets the menu structure for Apply command.
        /// </summary>
        public MenuItemDefinition GetApplyMenu()
        {
            Debug.WriteLine("MenuBuilder.GetApplyMenu");
            
            return new MenuItemDefinition
            {
                Title = "Apply",
                Description = "Apply profiling configuration",
                Handler = async () =>
                {
                    // TODO: Get profile path and harness from user input
                    var profilePath = "";  // Placeholder
                    var harness = "";       // Placeholder
                    var result = await _commandInvoker.InvokeApplyAsync(profilePath, harness);
                    _resultPane.DisplayCommandResult(result, _commandInvoker.Output, _commandInvoker.ErrorOutput);
                }
            };
        }

        /// <summary>
        /// Gets the menu structure for Assess command.
        /// </summary>
        public MenuItemDefinition GetAssessMenu()
        {
            Debug.WriteLine("MenuBuilder.GetAssessMenu");
            
            return new MenuItemDefinition
            {
                Title = "Assess",
                Description = "Run assessment and publish results",
                Handler = async () =>
                {
                    // TODO: Get HF ID and publish flag from user input
                    var hfId = "";      // Placeholder
                    var publish = false; // Placeholder
                    var result = await _commandInvoker.InvokeAssessAsync(hfId, publish);
                    _resultPane.DisplayCommandResult(result, _commandInvoker.Output, _commandInvoker.ErrorOutput);
                }
            };
        }
    }

    /// <summary>
    /// Definition of a menu item with title, description, and handler.
    /// </summary>
    internal class MenuItemDefinition
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public Func<System.Threading.Tasks.Task>? Handler { get; set; }
    }
}

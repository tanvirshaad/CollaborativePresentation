// Clean, multi-use version for both single-slide and multi-slide editors
// Only export window.startDrawingInContainer, do not declare username globally, do not use Swal or document.ready

window.startDrawingInContainer = function(container, slideId, username, userRole) {
    console.log('Initializing editor', container, slideId, username, 'Role:', userRole);
    
    // Validate parameters
    if (!container) {
        console.error('Container is required but was not provided');
        if (typeof toastr !== 'undefined') {
            toastr.error('Drawing initialization failed: Container not provided');
        }
        return;
    }
    
    if (!slideId) {
        console.error('SlideId is required but was not provided');
        if (typeof toastr !== 'undefined') {
            toastr.error('Drawing initialization failed: Slide ID not provided');
        }
        return;
    }
    
    if (!username) {
        console.warn('Username not provided, using "Anonymous"');
        username = 'Anonymous';
    }
    
    if (!userRole) {
        console.warn('UserRole not provided, defaulting to "Viewer"');
        userRole = 'Viewer';
    }
    
    var editor;
    var connectionLoadCanvas;
    const isViewer = userRole === 'Viewer';

    function startDrawing() {
        try {
            console.log('Starting drawing initialization with container:', container);
            
            // Verify the container is valid and attached to the DOM
            if (!container || !document.body.contains(container)) {
                throw new Error('Container not found in DOM');
            }
            
            // Check if jsdraw is available
            if (typeof jsdraw === 'undefined' || !jsdraw.Editor) {
                throw new Error('JsDraw library not loaded properly');
            }
            
            // Create settings object
            const settings = {
                wheelEventsEnabled: 'only-if-focused',
                readOnly: isViewer, // Set readOnly to true for viewers
                tools: {
                    pen: {
                        enabled: !isViewer,
                        sizes: [1, 2, 3, 5, 8],
                        defaultSize: 2
                    },
                    eraser: {
                        enabled: !isViewer
                    },
                    selector: {
                        enabled: !isViewer
                    },
                    text: {
                        enabled: !isViewer
                    }
                }
            };
            
            console.log('Creating JsDraw Editor with settings:', settings);
            try {
                editor = new jsdraw.Editor(container, settings);
            } catch (editorError) {
                console.error('Error creating JsDraw Editor:', editorError);
                throw new Error('Could not initialize drawing editor: ' + editorError.message);
            }
            
            // Only add toolbar if not a viewer
            if (!isViewer) {
                const toolbar = editor.addToolbar();

            toolbar.addActionButton('Save', async () => {
                try {
                    const saveButton = document.querySelector('button.jsdraw-actionbutton');
                    if (saveButton) {
                        saveButton.disabled = true;
                        saveButton.textContent = 'Saving...';
                    }
                    
                    console.log('Save button clicked');
                    const success = await saveNewSvg();
                    
                    if (success) {
                        console.log('Save successful, recreating editor');
                        try {
                            editor.remove();
                            setTimeout(() => {
                                startDrawing();
                            }, 500); // Add a small delay to ensure clean recreation
                        } catch (recreateError) {
                            console.error('Error recreating editor:', recreateError);
                            location.reload(); // Last resort: refresh the page
                        }
                    } else {
                        console.warn('Save was not successful');
                        if (saveButton) {
                            saveButton.disabled = false;
                            saveButton.textContent = 'Save';
                        }
                    }
                } catch (error) {
                    console.error('Error during save:', error);
                    toastr.error('Failed to save slide: ' + (error.message || 'Unknown error'));
                    const saveButton = document.querySelector('button.jsdraw-actionbutton');
                    if (saveButton) {
                        saveButton.disabled = false;
                        saveButton.textContent = 'Save';
                    }
                }
            });                toolbar.addActionButton('Download⬇️', () => {
                    var jpgDataUrl = editor.toDataURL();
                    download(jpgDataUrl, `drawing-${slideId}.jpg`);
                });
            } else {
                // Add a viewer notification
                const viewerMsg = document.createElement('div');
                viewerMsg.className = 'viewer-mode-notice';
                viewerMsg.style.padding = '5px 10px';
                viewerMsg.style.backgroundColor = '#f8f9fa';
                viewerMsg.style.border = '1px solid #ddd';
                viewerMsg.style.borderRadius = '4px';
                viewerMsg.style.marginBottom = '10px';
                viewerMsg.style.textAlign = 'center';
                viewerMsg.textContent = 'View-only mode: You do not have permission to edit this presentation';
                container.insertBefore(viewerMsg, container.firstChild);
            }

            editor.getRootElement().style.height = '95vh';
            editor.getRootElement().style.border = '2px solid gray';
            editor.getRootElement().style.background = '#fff';

            const addToHistory = false;
            editor.dispatch(editor.setBackgroundStyle({
                autoresize: true,
            }), addToHistory);

            getExistingSvg();

            // Only set up drawing event listeners if not in viewer mode
            if (!isViewer) {
                editor.notifier.on(jsdraw.EditorEventType.CommandDone, (evt) => {
                    if (evt.kind !== jsdraw.EditorEventType.CommandDone) {
                        throw new Error('Incorrect event type');
                    }

                    if (evt.command instanceof jsdraw.SerializableCommand) {
                        postToServer(JSON.stringify({
                            command: evt.command.serialize()
                        }));
                    } else {
                        console.log('!', evt.command, 'instanceof jsdraw.SerializableCommand');
                    }
                });

                editor.notifier.on(jsdraw.EditorEventType.CommandUndone, (evt) => {
                    if (evt.kind !== jsdraw.EditorEventType.CommandUndone) {
                        return;
                    }

                    if (!(evt.command instanceof jsdraw.SerializableCommand)) {
                        console.log('Not serializable!', evt.command);
                        return;
                    }

                    postToServer(JSON.stringify({
                        command: jsdraw.invertCommand(evt.command).serialize()
                    }));
                });
            }
        } catch (error) {
            console.error('Error initializing editor:', error);
        }
    }

    function startCommentConnection() {
        console.log('Starting SignalR connection for slide:', slideId);
        
        // First check if SignalR is available
        if (typeof signalR === 'undefined') {
            console.error('SignalR library not loaded');
            toastr.error('Error: SignalR library not loaded. Please refresh the page.');
            
            // Register a callback to initialize when SignalR loads
            window.onSignalRLoaded = function() {
                console.log('SignalR loaded asynchronously, retrying connection');
                startCommentConnection();
            };
            return;
        }
        
        // Close any existing connection before starting a new one
        if (connectionLoadCanvas) {
            try {
                console.log('Stopping existing connection');
                connectionLoadCanvas.stop();
            } catch (e) {
                console.warn('Error stopping previous connection:', e);
            }
        }

        // Build connection with more robust options
        console.log('Building new SignalR connection');
        try {
            connectionLoadCanvas = new signalR.HubConnectionBuilder()
                .withUrl("/presentationHub")
                .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 15000]) // More retry attempts
                .configureLogging(signalR.LogLevel.Debug) // Increase logging level for debugging
                .build();
        } catch (error) {
            console.error('Error building SignalR connection:', error);
            toastr.error('Failed to initialize connection. Please refresh the page.');
            return;
        }

        // Handle drawing updates
        connectionLoadCanvas.on("UpdateDrawing", (drawingData) => {
            console.log("Received drawing update");
            processUpdates(drawingData);
        });

        // Handle successful saves
        connectionLoadCanvas.on("SvgSaved", (savedSlideId) => {
            console.log(`SvgSaved event received for slide ${savedSlideId}, current slide: ${slideId}`);
            if (savedSlideId === slideId.toString()) {
                console.log('Slide saved broadcast received');
                // Don't show success here as it's already handled in saveNewSvg
            }
        });

        // Direct save successful notification
        connectionLoadCanvas.on("SaveSuccessful", (savedSlideId) => {
            console.log(`SaveSuccessful event received for slide ${savedSlideId}`);
            // This will be handled by the promise in saveNewSvg
        });

        // Handle errors from the server
        connectionLoadCanvas.on("Error", (errorMessage) => {
            console.error(`SignalR Error: ${errorMessage}`);
            toastr.error(errorMessage);
        });

        // Handle connection state changes
        connectionLoadCanvas.onreconnecting(error => {
            console.warn('SignalR reconnecting:', error);
            toastr.warning('Connection lost. Reconnecting to server...');
            // Disable UI during reconnection
            document.getElementById('presentation-content').style.opacity = '0.5';
        });
        
        connectionLoadCanvas.onreconnected(connectionId => {
            console.log('SignalR reconnected with ID:', connectionId);
            toastr.success('Reconnected to server');
            // Re-enable UI
            document.getElementById('presentation-content').style.opacity = '1.0';
            // Re-join the board to ensure we're in the correct group
            connectionLoadCanvas.invoke("JoinBoard", slideId.toString(), username)
                .catch(err => console.error("Error rejoining board:", err));
        });

        connectionLoadCanvas.onclose(error => {
            console.error('SignalR connection closed:', error);
            toastr.error('Connection lost. Please refresh the page.');
            document.getElementById('presentation-content').style.opacity = '0.5';
        });

        // Start the connection
        console.log("Starting SignalR connection...");
        connectionLoadCanvas.start()
            .then(() => {
                console.log("SignalR Connected");
                return connectionLoadCanvas.invoke("JoinBoard", slideId.toString(), username);
            })
            .then((msg) => {
                console.log("Joined board:", msg);
                document.getElementById('presentation-content').style.opacity = '1.0';
                toastr.success('Connected to server');
            })
            .catch(err => {
                console.error("SignalR Connection Error:", err);
                toastr.error("Failed to connect to server. Please refresh the page and try again.");
            });
    }

    function getExistingSvg() {
        fetch(`/Home/GetSlideData?slideId=${slideId}`)
            .then(response => response.json())
            .then(data => {
                if (data.success && data.svgData && data.svgData.length > 0) {
                    editor.loadFromSVG(data.svgData);
                }
            })
            .catch(error => {
                console.error('Error retrieving SVG data:', error);
            });
    }

    const postToServer = async (commandData) => {
        try {
            await connectionLoadCanvas.invoke("Modify", slideId.toString(), commandData);
        } catch (e) {
            console.error('Error posting command', e);
            // The hub will send an Error message which will be handled by the Error event listener
        }
    };

    const processUpdates = async (drawingData) => {
        try {
            const json = JSON.parse(drawingData);
            try {
                const command = jsdraw.SerializableCommand.deserialize(json.command, editor);
                await command.apply(editor);
            } catch (e) {
                console.warn('Error parsing command', e);
            }
        } catch (e) {
            console.error('Error fetching updates', e);
        }
    };

    async function saveNewSvg() {
        try {
            console.log('Starting saveNewSvg for slide:', slideId);
            
            // Add a spinner or indicator that saving is in progress
            toastr.info('Saving slide...');

            // Get SVG data from the editor
            console.log('Generating SVG data...');
            let svgData;
            try {
                svgData = await editor.toSVGAsync();
                if (!svgData) {
                    console.error('Editor returned null SVG data');
                    toastr.error('Failed to generate SVG data');
                    return false;
                }
            } catch (svgError) {
                console.error('Error generating SVG data:', svgError);
                toastr.error('Failed to generate SVG data');
                return false;
            }
            
            // Convert to string
            let svgString;
            try {
                svgString = svgData.outerHTML;
                
                if (!svgString || !svgString.includes('<svg')) {
                    console.error('Invalid SVG data generated:', svgString);
                    toastr.error('Generated SVG data is invalid');
                    return false;
                }
                
                console.log('Generated valid SVG data with size:', svgString.length);
            } catch (conversionError) {
                console.error('Error converting SVG to string:', conversionError);
                toastr.error('Error preparing SVG data');
                return false;
            }
            
            // First try using SignalR
            let saveSuccessful = false;
            
            // Check if we have an active SignalR connection
            if (connectionLoadCanvas && connectionLoadCanvas.state === signalR.HubConnectionState.Connected) {
                try {
                    console.log('Attempting to save via SignalR...');
                    // Create a promise that will be resolved when we get a response
                    const savePromise = new Promise((resolve, reject) => {
                        // Setup one-time event handlers for success/error responses
                        const successHandler = (savedSlideId) => {
                            if (savedSlideId === slideId.toString()) {
                                console.log('SaveSuccessful event received');
                                connectionLoadCanvas.off("SaveSuccessful", successHandler);
                                connectionLoadCanvas.off("Error", errorHandler);
                                resolve(true);
                            }
                        };
                        
                        const errorHandler = (message) => {
                            console.error('Error event received:', message);
                            // Only handle this error if it's related to saving
                            if (message.includes('save') || message.includes('SVG') || message.includes('database')) {
                                connectionLoadCanvas.off("SaveSuccessful", successHandler);
                                connectionLoadCanvas.off("Error", errorHandler);
                                reject(new Error(message));
                            }
                        };
                        
                        // Register handlers
                        connectionLoadCanvas.on("SaveSuccessful", successHandler);
                        connectionLoadCanvas.on("Error", errorHandler);
                        
                        // Start the save operation
                        console.log('Invoking SaveSvg method...');
                        connectionLoadCanvas.invoke("SaveSvg", slideId.toString(), svgString)
                            .catch(error => {
                                console.error('Error during SaveSvg invoke:', error);
                                connectionLoadCanvas.off("SaveSuccessful", successHandler);
                                connectionLoadCanvas.off("Error", errorHandler);
                                reject(error);
                            });
                        
                        // Set a timeout to reject the promise if no response is received
                        setTimeout(() => {
                            connectionLoadCanvas.off("SaveSuccessful", successHandler);
                            connectionLoadCanvas.off("Error", errorHandler);
                            reject(new Error('Save operation timed out after 5 seconds'));
                        }, 5000);
                    });
                    
                    // Wait for save operation to complete with timeout
                    await Promise.race([
                        savePromise,
                        new Promise((_, reject) => setTimeout(() => reject(new Error('Save operation timed out')), 5000))
                    ]);
                    
                    saveSuccessful = true;
                    console.log('Save via SignalR successful');
                } catch (signalRError) {
                    console.error('SignalR save failed:', signalRError);
                    // SignalR save failed, we'll try HTTP fallback
                }
            } else {
                console.warn('No active SignalR connection, will use HTTP fallback');
            }
            
            // If SignalR save failed, try HTTP fallback
            if (!saveSuccessful) {
                console.log('Attempting to save via HTTP fallback...');
                try {
                    const response = await fetch('/Presentation/SaveSlide', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-Requested-With': 'XMLHttpRequest',
                            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                        },
                        body: JSON.stringify({
                            slideId: parseInt(slideId),
                            svgData: svgString
                        })
                    });
                    
                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }
                    
                    const result = await response.json();
                    if (!result.success) {
                        throw new Error(result.message || 'Unknown error');
                    }
                    
                    saveSuccessful = true;
                    console.log('Save via HTTP successful');
                } catch (httpError) {
                    console.error('HTTP save failed:', httpError);
                    throw new Error(`Failed to save via both SignalR and HTTP: ${httpError.message}`);
                }
            }
            
            // Only update thumbnail if save was successful
            if (saveSuccessful) {
                try {
                    console.log('Updating thumbnail...');
                    const slideThumbnail = document.querySelector(`[data-slide-id="${slideId}"]`);
                    if (slideThumbnail) {
                        const img = document.createElement('img');
                        // Use try-catch for the base64 conversion which can sometimes fail
                        try {
                            img.src = `data:image/svg+xml;base64,${btoa(unescape(encodeURIComponent(svgString)))}`;
                        } catch (base64Error) {
                            console.warn('Failed to create base64 thumbnail, using placeholder', base64Error);
                            img.src = 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI4MDAiIGhlaWdodD0iNjAwIiB2aWV3Qm94PSIwIDAgODAwIDYwMCI+PC9zdmc+';
                        }
                        img.alt = `Slide ${slideThumbnail.getAttribute('data-slide-number')}`;
                        img.style.width = '100%';
                        img.style.height = '100%';
                        img.style.objectFit = 'contain';
                        
                        // Clear the loading text and add the image
                        slideThumbnail.innerHTML = '';
                        slideThumbnail.appendChild(img);
                    }
                } catch (thumbError) {
                    console.error('Error updating thumbnail:', thumbError);
                    // Continue even if thumbnail update fails
                }
                
                console.log('Save operation completed successfully');
                toastr.success('Slide saved successfully!');
                return true;
            } else {
                throw new Error('Save operation failed');
            }
        } catch (error) {
            console.error('Error saving SVG:', error);
            toastr.error(`Failed to save slide: ${error.message || 'Unknown error'}`);
            return false;
        }
    }

    function download(dataurl, filename) {
        const link = document.createElement("a");
        link.href = dataurl;
        link.download = filename;
        link.click();
    }

    // Start everything with proper initialization sequence
    try {
        console.log('Starting drawing initialization sequence for slide:', slideId);
        
        // Check for library availability with more detailed messages
        let missingLibraries = [];
        
        if (typeof jsdraw === 'undefined') {
            missingLibraries.push('Drawing library (JsDraw)');
        }
        
        if (typeof signalR === 'undefined') {
            missingLibraries.push('Communication library (SignalR)');
        }
        
        if (missingLibraries.length > 0) {
            const errorMsg = 'Missing required libraries: ' + missingLibraries.join(', ');
            console.error(errorMsg);
            toastr.error(errorMsg + '. Please refresh the page.');
            return;
        }
        
        // Initialize the editor first since it doesn't depend on SignalR
        startDrawing();
        
        // Then connect to SignalR
        setTimeout(() => {
            try {
                startCommentConnection();
                console.log('SignalR connection initialized successfully');
            } catch (signalRError) {
                console.error('Error initializing SignalR connection:', signalRError);
                toastr.warning('Communication initialization failed. Some features may not work properly.');
                // Continue even if SignalR fails - the editor should still work in local-only mode
            }
        }, 200); // Slight delay to ensure editor is ready first
        
        console.log('Drawing initialization completed successfully');
    } catch (error) {
        console.error('Error during drawing initialization:', error);
        toastr.error('Failed to initialize slide editor: ' + error.message);
    }
}

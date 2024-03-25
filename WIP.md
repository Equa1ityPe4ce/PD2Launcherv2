<GroupBox Header="DDraw Options" Margin="10" BorderThickness="0" 
                          FontFamily="{StaticResource BlizzHeavy}" Foreground="{StaticResource GoldBrush}" 
                          FontSize="16" Visibility="{Binding Is3DfxSelected, Converter={StaticResource BoolToVis}}">
                    <StackPanel>
                        <TextBlock Text="Mode" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}"/>
                        <ComboBox x:Name="Mode" FocusVisualStyle="{x:Null}"
                                  HorizontalAlignment="Left"
                                  ItemsSource="{Binding OptionsModePicker}"
                                  Width="175"
                                  SelectedValuePath="ActualValue" 
                                  DisplayMemberPath="DisplayValue" />
                    </StackPanel>

                    <TextBlock Text="Width" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}"/>
                <TextBox x:Name="Width" Style="{StaticResource CustomTextBoxStyle}"/>

                <TextBlock Text="Height" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}"/>
                <TextBox x:Name="Height" Style="{StaticResource CustomTextBoxStyle}" />

                <CheckBox Content="Enable Advanced Options"  Style="{StaticResource CustomCheckBoxStyle}" 
                              FontFamily="{StaticResource BlizzMedium}" Foreground="{StaticResource GoldDarkerBrush}" 
                              FontSize="12" Margin="10,10,10,0"/>

                    <StackPanel Visibility="{Binding IsChecked, ElementName=showAdvancedOptions, Converter={StaticResource BoolToVis}}">
                        <TextBlock Text="Window Position" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}"/>

                        <TextBlock Text="-320000 = Center To Screen" Foreground="{StaticResource GoldDarkerBrush}" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}"/>

                        <TextBlock Text="Vertical" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}"/>

                        <TextBox x:Name="ddrawPosX" Style="{StaticResource CustomTextBoxStyle}" />

                        <TextBlock Text="Horizontal" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}"/>

                        <TextBox x:Name="ddrawPosY" Style="{StaticResource CustomTextBoxStyle}" />
                        <CheckBox x:Name="maintasCheckBox" Content="Maintain Aspect Ratio" Style="{StaticResource CustomCheckBoxStyle}"
                              FontFamily="{StaticResource BlizzMedium}" Foreground="{StaticResource GoldDarkerBrush}" 
                              FontSize="12" Margin="10,10,10,0"/>

                        <CheckBox x:Name="boxingCheckBox" Content="Windowboxing / Integer Scaling" Style="{StaticResource CustomCheckBoxStyle}"
                              FontFamily="{StaticResource BlizzMedium}" Foreground="{StaticResource GoldDarkerBrush}" 
                              FontSize="12" Margin="10,10,10,0"/>

                        <CheckBox x:Name="adjmouseCheckBox"
                      Content="Automatic Mouse Sensitivity" Style="{StaticResource CustomCheckBoxStyle}"
                              FontFamily="{StaticResource BlizzMedium}" Foreground="{StaticResource GoldDarkerBrush}" 
                              FontSize="12" Margin="10,10,10,0"/>

                        <CheckBox x:Name="vsyncCheckBox"
                          Content="Vertical Sync" Style="{StaticResource CustomCheckBoxStyle}"
                              FontFamily="{StaticResource BlizzMedium}" Foreground="{StaticResource GoldDarkerBrush}" 
                              FontSize="12" Margin="10,10,10,0"/>
                        <TextBlock Text="Only works if full screen is enabled" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}"/>

                        <!-- CustomCheckBox: devmodeCheckBox -->
                        <CheckBox x:Name="devmodeCheckBox"
                          Content="Unlock cursor" Style="{StaticResource CustomCheckBoxStyle}"
                              FontFamily="{StaticResource BlizzMedium}" Foreground="{StaticResource GoldDarkerBrush}" 
                              FontSize="12" Margin="10,10,10,0"/>

                        <!-- CustomCheckBox: borderCheckBox -->
                        <CheckBox x:Name="borderCheckBox"
                          Content="Show Window Borders" Style="{StaticResource CustomCheckBoxStyle}"
                              FontFamily="{StaticResource BlizzMedium}" Foreground="{StaticResource GoldDarkerBrush}" 
                              FontSize="12" Margin="10,10,10,0"/>

                        <!-- CustomCheckBox: resizeableCheckBox -->
                        <CheckBox x:Name="resizeableCheckBox"
                          Content="Resizable window" Style="{StaticResource CustomCheckBoxStyle}"
                              FontFamily="{StaticResource BlizzMedium}" Foreground="{StaticResource GoldDarkerBrush}" 
                              FontSize="12" Margin="10,10,10,0"/>

                        <!-- Button for Showing Advanced Options -->
                        <Button x:Name="ShowAdvancedOptionsButton"
                        Content="Show Advanced Options"
                        Background="Transparent"
                        Foreground="{StaticResource GoldLighterBrush}"
                        Margin="0,0,0,20"/>
                    </StackPanel>
                </GroupBox>

                <!-- Advanced Options Layout (initially hidden) -->
                        <StackPanel x:Name="AdvancedOptionsLayout" Visibility="Collapsed">
                            <TextBlock Text="Read ddraw.ini for info on advanced options"
                            Margin="190,-30,0,0" FontStyle="Italic"/>

                            <TextBlock Text="Rendering rate (Max FPS)" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}"/>
                            <!-- ComboBox for maxfpsPicker -->
                            <ComboBox x:Name="maxfpsPicker"
                              Style="{StaticResource CustomComboBoxStyle}"
                              ItemsSource="{Binding MaxFpsPickerItems}"
                              SelectedValuePath="ActualValue" 
                              DisplayMemberPath="DisplayValue"
                              Margin="90,0,0,0"/>
                            <TextBlock Text="Does not have an impact on the game speed"
                           FontStyle="Italic"
                           Foreground="{StaticResource GoldDarkerBrush}"
                           Margin="90,0,0,0"/>

                            <!-- Max game ticks -->
                            <TextBlock Text="Max game ticks" Style="{StaticResource OptionsTextBlockHeaderStyle}" Margin="90,10,0,0"/>
                            <ComboBox x:Name="maxgameticksPicker"
              Style="{StaticResource CustomComboBoxStyle}"
              ItemsSource="{Binding MaxGameTicksPickerItems}"
                                SelectedValuePath="ActualValue" 
              DisplayMemberPath="DisplayValue"
              Margin="90,0,0,0"/>
                            <TextBlock Text="Fix for flickering (lower = slower)"
           Style="{StaticResource OptionsTextBlockHeaderStyle}"
           Foreground="{StaticResource GoldDarkerBrush}"
           Margin="90,0,0,0" />

                            <!-- Save Window Position -->
                            <TextBlock Text="Save Window Position" Style="{StaticResource OptionsTextBlockHeaderStyle}" Margin="90,10,0,0" />
                            <ComboBox x:Name="savesettingsPicker"
          Style="{StaticResource CustomComboBoxStyle}"
          ItemsSource="{Binding SaveSettingsPickerItems}"
                                SelectedValuePath="ActualValue" 
          DisplayMemberPath="DisplayValue"
          Margin="90,0,0,0"/>
                            <TextBlock Text="Save window position/size"
           Style="{StaticResource OptionsTextBlockHeaderStyle}"
           Foreground="{StaticResource GoldDarkerBrush}"
           Margin="90,0,0,0" />

                            <!-- Renderer -->
                            <TextBlock Text="Renderer" Style="{StaticResource OptionsTextBlockHeaderStyle}" Margin="90,10,0,0" />
                            <ComboBox x:Name="rendererPicker"
              Style="{StaticResource CustomComboBoxStyle}"
              ItemsSource="{Binding RendererPickerItems}"
                                    SelectedValuePath="ActualValue" 
                                      DisplayMemberPath="DisplayValue"
                                      Margin="90,0,0,0"/>
                            <TextBlock Text="Auto will try OpenGL, Direct3D, finally GDI"
                               Style="{StaticResource OptionsTextBlockHeaderStyle}"
                               Foreground="{StaticResource GoldDarkerBrush}"
                               Margin="90,0,0,0" />     

                            <!-- Windows API Hooking -->
                            <TextBlock Text="Windows API Hooking" Style="{StaticResource OptionsTextBlockHeaderStyle}" Margin="90,10,0,0" />
                            <ComboBox x:Name="hookPicker"
                                    Style="{StaticResource CustomComboBoxStyle}"
                                    ItemsSource="{Binding HookPickerItems}"
                                    SelectedValuePath="ActualValue" 
                                    DisplayMemberPath="DisplayValue"
                                    Margin="90,0,0,0"/>
                            <TextBlock Text="Detours can help with problems, requires rendering GDI"
                               Style="{StaticResource OptionsTextBlockHeaderStyle}"
                               Foreground="{StaticResource GoldDarkerBrush}"
                               Margin="90,0,0,0" />

                            <TextBlock Text="Force Minimum FPS" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}" />
                            <ComboBox x:Name="minfpsPicker"
                              Style="{StaticResource CustomComboBoxStyle}"
                              ItemsSource="{Binding MinFpsPickerItems}"
                              SelectedValuePath="ActualValue"
                              DisplayMemberPath="DisplayValue"
                              Margin="90,0,0,0"/>
                            <TextBlock Text="Use 5/10 for display issues(menus/loading screens)"
                               FontStyle="Italic"
                               Foreground="{StaticResource GoldDarkerBrush}"
                               Margin="90,0,0,0" />

                            <!-- Preliminary Shade Support -->
                            <TextBlock Text="Preliminary Shade Support" FontFamily="{StaticResource BlizzMedium}" Style="{StaticResource OptionsTextBlockHeaderStyle}" />
                            <ComboBox x:Name="shaderPicker"
                              Style="{StaticResource CustomComboBoxStyle}"
                              ItemsSource="{Binding ShaderPickerItems}"
                              SelectedValuePath="ActualValue"
                              DisplayMemberPath="DisplayValue"
                              Margin="90,0,0,0"/>
                            <TextBlock Text="Requires Renderer OpenGL"
                               FontStyle="Italic"
                               Foreground="{StaticResource GoldDarkerBrush}"
                               Margin="90,0,0,0" />

                            <!-- Custom CheckBoxes -->
                            <CheckBox x:Name="d3d9linearCheckBox"
                              Content="Enable d3d9linear"
                              Style="{StaticResource CustomCheckBoxStyle}"
                              Margin="90,20,0,0" />
                            <TextBlock Text="Upscaling filter for the direct3d9 renderer"
                               FontStyle="Italic"
                               Foreground="{StaticResource GoldDarkerBrush}"
                               Margin="90,0,0,0" />

                            <CheckBox x:Name="singlecpuCheckBox"
                              Content="Force CPU0 affinity"
                              Style="{StaticResource CustomCheckBoxStyle}"
                              Margin="90,10,0,0" />
                            <TextBlock Text="Avoids crashes/freezing can affect performance"
                           FontStyle="Italic"
                           Foreground="{StaticResource GoldDarkerBrush}"
                           Margin="90,0,0,0" />
                        </StackPanel>
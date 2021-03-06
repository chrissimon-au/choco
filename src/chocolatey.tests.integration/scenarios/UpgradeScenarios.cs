﻿// Copyright © 2011 - Present RealDimensions Software, LLC
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
// You may obtain a copy of the License at
// 
// 	http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace chocolatey.tests.integration.scenarios
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using NuGet;
    using Should;
    using bdddoc.core;
    using chocolatey.infrastructure.app.commands;
    using chocolatey.infrastructure.app.configuration;
    using chocolatey.infrastructure.app.services;
    using chocolatey.infrastructure.results;

    public class UpgradeScenarios
    {
        public abstract class ScenariosBase : TinySpec
        {
            protected ConcurrentDictionary<string, PackageResult> Results;
            protected ChocolateyConfiguration Configuration;
            protected IChocolateyPackageService Service;

            public override void Context()
            {
                Configuration = Scenario.upgrade();
                Scenario.reset(Configuration);
                Configuration.PackageNames = Configuration.Input = "upgradepackage";
                Scenario.add_packages_to_source_location(Configuration, Configuration.Input + "*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "installpackage*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "badpackage*" + Constants.PackageExtension);
                Scenario.install_package(Configuration, "installpackage", "1.0.0");
                Scenario.install_package(Configuration, "upgradepackage", "1.0.0");
                Configuration.SkipPackageInstallProvider = true;
                Scenario.install_package(Configuration, "badpackage", "1.0");
                Configuration.SkipPackageInstallProvider = false;

                Service = NUnitSetup.Container.GetInstance<IChocolateyPackageService>();
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_noop_upgrading_a_package_that_has_available_upgrades : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                Configuration.Noop = true;
            }

            public override void Because()
            {
                Service.upgrade_noop(Configuration);
            }

            [Fact]
            public void should_contain_older_version_in_directory()
            {
                var shimFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "console.exe");

                File.ReadAllText(shimFile).ShouldEqual("1.0.0");
            }

            [Fact]
            public void should_contain_a_message_that_a_new_version_is_available()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("You have upgradepackage v1.0.0 installed. Version 1.1.0 is available based on your source(s)")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_contain_a_message_that_a_package_can_be_upgraded()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("can upgrade 1/1")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_not_create_a_rollback()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bkp", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_noop_upgrading_a_package_that_does_not_have_available_upgrades : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                Configuration.Noop = true;
                Configuration.PackageNames = Configuration.Input = "installpackage";
            }

            public override void Because()
            {
                Service.upgrade_noop(Configuration);
            }

            [Fact]
            public void should_contain_a_message_that_you_have_the_latest_version_available()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Info).or_empty_list_if_null())
                {
                    if (message.Contains("installpackage v1.0.0 is the latest version available based on your source(s)")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_contain_a_message_that_no_packages_can_be_upgraded()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("can upgrade 0/1")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_not_create_a_rollback()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bkp", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_noop_upgrading_a_package_that_does_not_exist : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                Configuration.Noop = true;
                Configuration.PackageNames = Configuration.Input = "nonexistingpackage";
            }

            public override void Because()
            {
                Service.upgrade_noop(Configuration);
            }

            [Fact]
            public void should_contain_a_message_the_package_was_not_found()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Error).or_empty_list_if_null())
                {
                    if (message.Contains("nonexistingpackage not installed. The package was not found with the source(s) listed")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_contain_a_message_that_no_packages_can_be_upgraded()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("can upgrade 0/0")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_an_existing_package_happy_path : ScenariosBase
        {
            private PackageResult _packageResult;

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                _packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_upgrade_where_install_location_reports()
            {
                Directory.Exists(_packageResult.InstallLocation).ShouldBeTrue();
            }

            [Fact]
            public void should_upgrade_a_package_in_the_lib_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_delete_the_rollback()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bkp", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_contain_newer_version_in_directory()
            {
                var shimFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "console.exe");

                File.ReadAllText(shimFile).ShouldEqual("1.1.0");
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, Configuration.PackageNames + Constants.PackageExtension);
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.1.0.0");
            }

            [Fact]
            public void should_contain_a_warning_message_that_it_upgraded_successfully()
            {
                bool upgradedSuccessMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 1/1")) upgradedSuccessMessage = true;
                }

                upgradedSuccessMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_contain_a_warning_message_with_old_and_new_versions()
            {
                bool upgradeMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("You have upgradepackage v1.0.0 installed. Version 1.1.0 is available based on your source")) upgradeMessage = true;
                }

                upgradeMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                _packageResult.Success.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                _packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                _packageResult.Warning.ShouldBeFalse();
            }

            [Fact]
            public void config_should_match_package_result_name()
            {
                _packageResult.Name.ShouldEqual(Configuration.PackageNames);
            }

            [Fact]
            public void should_match_the_upgrade_version_of_one_dot_one_dot_zero()
            {
                _packageResult.Version.ShouldEqual("1.1.0");
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_force_upgrading_a_package : ScenariosBase
        {
            private PackageResult _packageResult;

            public override void Context()
            {
                base.Context();
                Configuration.Force = true;
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                _packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_upgrade_where_install_location_reports()
            {
                Directory.Exists(_packageResult.InstallLocation).ShouldBeTrue();
            }

            [Fact]
            public void should_upgrade_a_package_in_the_lib_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, Configuration.PackageNames + Constants.PackageExtension);
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.1.0.0");
            }

            [Fact]
            public void should_delete_the_rollback()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bkp", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_contain_newer_version_in_directory()
            {
                var shimFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "console.exe");

                File.ReadAllText(shimFile).ShouldEqual("1.1.0");
            }

            [Fact]
            public void should_contain_a_warning_message_that_it_upgraded_successfully()
            {
                bool upgradedSuccessMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 1/1")) upgradedSuccessMessage = true;
                }

                upgradedSuccessMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_contain_a_warning_message_with_old_and_new_versions()
            {
                bool upgradeMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("You have upgradepackage v1.0.0 installed. Version 1.1.0 is available based on your source")) upgradeMessage = true;
                }

                upgradeMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                _packageResult.Success.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                _packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                _packageResult.Warning.ShouldBeFalse();
            }

            [Fact]
            public void config_should_match_package_result_name()
            {
                _packageResult.Name.ShouldEqual(Configuration.PackageNames);
            }

            [Fact]
            public void should_match_the_upgrade_version_of_one_dot_one_dot_zero()
            {
                _packageResult.Version.ShouldEqual("1.1.0");
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_that_does_not_have_available_upgrades : ScenariosBase
        {
            private PackageResult _packageResult;

            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "installpackage";
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                _packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_contain_a_message_that_you_have_the_latest_version_available()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Info).or_empty_list_if_null())
                {
                    if (message.Contains("installpackage v1.0.0 is the latest version available based on your source(s)")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_contain_a_message_that_no_packages_were_upgraded()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 0/1 ")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_not_create_a_rollback()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bkp", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_not_remove_the_package_from_the_lib_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_be_the_same_version_of_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, Configuration.PackageNames + Constants.PackageExtension);
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                _packageResult.Success.ShouldBeTrue();
            }

            [Fact]
            public void should_have_inconclusive_package_result()
            {
                _packageResult.Inconclusive.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                _packageResult.Warning.ShouldBeFalse();
            }

            [Fact]
            public void should_match_the_existing_version_of_one_dot_zero_dot_zero()
            {
                _packageResult.Version.ShouldEqual("1.0.0");
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_force_upgrading_a_package_that_does_not_have_available_upgrades : ScenariosBase
        {
            private PackageResult _packageResult;

            public override void Context()
            {
                base.Context();
                Configuration.Force = true;
                Configuration.PackageNames = Configuration.Input = "installpackage";
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                _packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_contain_a_message_that_you_have_the_latest_version_available()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Info).or_empty_list_if_null())
                {
                    if (message.Contains("installpackage v1.0.0 is the latest version available based on your source(s)")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_contain_a_message_that_the_package_was_upgraded()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 1/1")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_not_create_a_rollback()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bkp", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_not_remove_the_package_from_the_lib_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_be_the_same_version_of_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, Configuration.PackageNames + Constants.PackageExtension);
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                _packageResult.Success.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                _packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                _packageResult.Warning.ShouldBeFalse();
            }

            [Fact]
            public void should_match_the_existing_version_of_one_dot_zero_dot_zero()
            {
                _packageResult.Version.ShouldEqual("1.0.0");
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_packages_with_packages_config : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                var packagesConfig = "{0}\\context\\testing.packages.config".format_with(Scenario.get_top_level());
                Configuration.PackageNames = Configuration.Input = packagesConfig;
            }

            public override void Because()
            {
            }

            [Fact]
            [ExpectedException(typeof (ApplicationException))]
            public void should_throw_an_error_that_it_is_not_allowed()
            {
                Results = Service.upgrade_run(Configuration);
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_with_a_read_and_delete_share_locked_file : ScenariosBase
        {
            private PackageResult _packageResult;

            private FileStream fileStream;

            public override void Context()
            {
                base.Context();
                var fileToOpen = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "chocolateyInstall.ps1");
                fileStream = new FileStream(fileToOpen, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete);
            }

            public override void AfterObservations()
            {
                base.AfterObservations();
                fileStream.Close();
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                _packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_upgrade_where_install_location_reports()
            {
                Directory.Exists(_packageResult.InstallLocation).ShouldBeTrue();
            }

            [Fact]
            public void should_upgrade_a_package_in_the_lib_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, Configuration.PackageNames + Constants.PackageExtension);
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.1.0.0");
            }

            [Fact]
            public void should_not_be_able_delete_the_rollback()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bkp", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_contain_newer_version_in_directory()
            {
                var shimFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "console.exe");

                File.ReadAllText(shimFile).ShouldEqual("1.1.0");
            }

            [Fact]
            public void should_contain_a_warning_message_that_it_upgraded_successfully()
            {
                bool upgradedSuccessMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 1/1")) upgradedSuccessMessage = true;
                }

                upgradedSuccessMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_contain_a_warning_message_with_old_and_new_versions()
            {
                bool upgradeMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("You have upgradepackage v1.0.0 installed. Version 1.1.0 is available based on your source")) upgradeMessage = true;
                }

                upgradeMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                _packageResult.Success.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                _packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                _packageResult.Warning.ShouldBeFalse();
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_with_an_exclusively_locked_file : ScenariosBase
        {
            private PackageResult _packageResult;

            private FileStream fileStream;

            public override void Context()
            {
                base.Context();
                var fileToOpen = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "chocolateyInstall.ps1");
                fileStream = new FileStream(fileToOpen, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }

            public override void AfterObservations()
            {
                base.AfterObservations();
                fileStream.Close();
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                _packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_have_a_package_installed_in_the_lib_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_delete_the_rollback()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bkp", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_contain_old_version_in_directory()
            {
                var shimFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "console.exe");

                File.ReadAllText(shimFile).ShouldEqual("1.0.0");
            }

            [Fact]
            public void should_not_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, Configuration.PackageNames + Constants.PackageExtension);
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_contain_a_warning_message_that_it_was_not_able_to_upgrade()
            {
                bool upgradedSuccessMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 0/1")) upgradedSuccessMessage = true;
                }

                upgradedSuccessMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_contain_a_warning_message_with_old_and_new_versions()
            {
                bool upgradeMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("You have upgradepackage v1.0.0 installed. Version 1.1.0 is available based on your source")) upgradeMessage = true;
                }

                upgradeMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_a_successful_package_result()
            {
                _packageResult.Success.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                _packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                _packageResult.Warning.ShouldBeFalse();
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_with_added_files : ScenariosBase
        {
            private PackageResult _packageResult;

            public override void Context()
            {
                base.Context();
                var fileAdded = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "dude.txt");
                File.WriteAllText(fileAdded, "hellow");
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                _packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_keep_the_added_file()
            {
                var fileAdded = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "dude.txt");

                File.Exists(fileAdded).ShouldBeTrue();
            }

            [Fact]
            public void should_contain_newer_version_in_directory()
            {
                var shimFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "console.exe");

                File.ReadAllText(shimFile).ShouldEqual("1.1.0");
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, Configuration.PackageNames + Constants.PackageExtension);
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.1.0.0");
            }

            [Fact]
            public void should_contain_a_warning_message_that_it_upgraded_successfully()
            {
                bool upgradedSuccessMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 1/1")) upgradedSuccessMessage = true;
                }

                upgradedSuccessMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                _packageResult.Success.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                _packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                _packageResult.Warning.ShouldBeFalse();
            }

            [Fact]
            public void should_match_the_upgrade_version_of_one_dot_one_dot_zero()
            {
                _packageResult.Version.ShouldEqual("1.1.0");
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_with_changed_files : ScenariosBase
        {
            private PackageResult _packageResult;

            public override void Context()
            {
                base.Context();
                var fileChanged = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "chocolateyInstall.ps1");
                File.WriteAllText(fileChanged, "hellow");
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                _packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_update_the_changed_file()
            {
                var fileChanged = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "chocolateyInstall.ps1");

                File.ReadAllText(fileChanged).ShouldNotEqual("hellow");
            }

            [Fact]
            public void should_contain_newer_version_in_directory()
            {
                var shimFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, "tools", "console.exe");

                File.ReadAllText(shimFile).ShouldEqual("1.1.0");
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, Configuration.PackageNames + Constants.PackageExtension);
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.1.0.0");
            }

            [Fact]
            public void should_contain_a_warning_message_that_it_upgraded_successfully()
            {
                bool upgradedSuccessMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 1/1")) upgradedSuccessMessage = true;
                }

                upgradedSuccessMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                _packageResult.Success.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                _packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                _packageResult.Warning.ShouldBeFalse();
            }

            [Fact]
            public void should_match_the_upgrade_version_of_one_dot_one_dot_zero()
            {
                _packageResult.Version.ShouldEqual("1.1.0");
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_that_does_not_exist : ScenariosBase
        {
            private PackageResult packageResult;

            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "nonexistingpackage";
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_not_install_a_package_in_the_lib_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_contain_a_message_the_package_was_not_found()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Error).or_empty_list_if_null())
                {
                    if (message.Contains("nonexistingpackage not installed. The package was not found with the source(s) listed")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_contain_a_message_that_no_packages_were_upgraded()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 0/1")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_a_successful_package_result()
            {
                packageResult.Success.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                packageResult.Warning.ShouldBeFalse();
            }

            [Fact]
            public void should_have_an_error_package_result()
            {
                bool errorFound = false;
                foreach (var message in packageResult.Messages)
                {
                    if (message.MessageType == ResultType.Error)
                    {
                        errorFound = true;
                    }
                }

                errorFound.ShouldBeTrue();
            }
            
            [Fact]
            public void should_have_expected_error_in_package_result()
            {
                bool errorFound = false;
                foreach (var message in packageResult.Messages)
                {
                    if (message.MessageType == ResultType.Error)
                    {
                        if (message.Message.Contains("The package was not found")) errorFound = true;
                    }
                }

                errorFound.ShouldBeTrue();
            }

        }

        [Concern(typeof(ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_that_is_not_installed : ScenariosBase
        {
            private PackageResult _packageResult;

            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "installpackage";
                Service.uninstall_run(Configuration);
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                _packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_install_where_install_location_reports()
            {
                Directory.Exists(_packageResult.InstallLocation).ShouldBeTrue();
            }

            [Fact]
            public void should_install_a_package_in_the_lib_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_a_rollback_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bkp", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_contain_a_warning_message_that_it_upgraded_successfully()
            {
                bool upgradedSuccessMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 1/1")) upgradedSuccessMessage = true;
                }

                upgradedSuccessMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                _packageResult.Success.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                _packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                _packageResult.Warning.ShouldBeFalse();
            }
        }     
        
        [Concern(typeof(ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_that_is_not_installed_and_failing_on_not_installed : ScenariosBase
        {
            private PackageResult _packageResult;

            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "installpackage";
                Service.uninstall_run(Configuration);
                Configuration.UpgradeCommand.FailOnNotInstalled = true;
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                _packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_not_install_a_package_in_the_lib_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }
            
            [Fact]
            public void should_contain_a_warning_message_that_it_was_unable_to_upgrade_a_package()
            {
                bool notInstalled = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("0/1")) notInstalled = true;
                }

                notInstalled.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_a_successful_package_result()
            {
                _packageResult.Success.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                _packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                _packageResult.Warning.ShouldBeFalse();
            }

            [Fact]
            public void should_have_an_error_package_result()
            {
                bool errorFound = false;
                foreach (var message in _packageResult.Messages)
                {
                    if (message.MessageType == ResultType.Error)
                    {
                        errorFound = true;
                    }
                }

                errorFound.ShouldBeTrue();
            }

            [Fact]
            public void should_have_expected_error_in_package_result()
            {
                bool errorFound = false;
                foreach (var message in _packageResult.Messages)
                {
                    if (message.MessageType == ResultType.Error)
                    {
                        if (message.Message.Contains("Cannot upgrade a non-existent package")) errorFound = true;
                    }
                }

                errorFound.ShouldBeTrue();
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_that_errors : ScenariosBase
        {
            private PackageResult packageResult;

            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "badpackage";
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
                packageResult = Results.FirstOrDefault().Value;
            }

            [Fact]
            public void should_not_remove_package_from_the_lib_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_not_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", Configuration.PackageNames, Configuration.PackageNames + Constants.PackageExtension);
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_put_the_package_in_the_lib_bad_directory()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bad", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_have_the_erroring_upgraded_package_in_the_lib_bad_directory()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib-bad", Configuration.PackageNames, Configuration.PackageNames + Constants.PackageExtension);
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("2.0.0.0");
            }

            [Fact]
            public void should_delete_the_rollback()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib-bkp", Configuration.PackageNames);

                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_contain_a_warning_message_that_it_was_unable_to_upgrade_a_package()
            {
                bool installedSuccessfully = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("0/1")) installedSuccessfully = true;
                }

                installedSuccessfully.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_a_successful_package_result()
            {
                packageResult.Success.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                packageResult.Inconclusive.ShouldBeFalse();
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                packageResult.Warning.ShouldBeFalse();
            }

            [Fact]
            public void should_have_an_error_package_result()
            {
                bool errorFound = false;
                foreach (var message in packageResult.Messages)
                {
                    if (message.MessageType == ResultType.Error)
                    {
                        errorFound = true;
                    }
                }

                errorFound.ShouldBeTrue();
            }

            [Fact]
            public void should_have_expected_error_in_package_result()
            {
                bool errorFound = false;
                foreach (var message in packageResult.Messages)
                {
                    if (message.MessageType == ResultType.Error)
                    {
                        if (message.Message.Contains("chocolateyInstall.ps1")) errorFound = true;
                    }
                }

                errorFound.ShouldBeTrue();
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_with_dependencies_happy : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "hasdependency";
                Scenario.add_packages_to_source_location(Configuration, "hasdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isexactversiondependency*" + Constants.PackageExtension);
                Scenario.install_package(Configuration, "isdependency", "1.0.0");
                Scenario.install_package(Configuration, "hasdependency", "1.0.0");
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency", "hasdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("2.1.0.0");
            }

            [Fact]
            public void should_upgrade_the_minimum_version_dependency()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isdependency", "isdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("2.0.0.0");
            }

            [Fact]
            public void should_upgrade_the_exact_version_dependency()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency", "isexactversiondependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("2.0.0.0");
            }

            [Fact]
            public void should_contain_a_message_that_everything_upgraded_successfully()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 3/3")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Success.ShouldBeTrue();
                }
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Inconclusive.ShouldBeFalse();
                }
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Warning.ShouldBeFalse();
                }
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_with_unavailable_dependencies : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "hasdependency";
                Scenario.add_packages_to_source_location(Configuration, "hasdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isexactversiondependency.1*" + Constants.PackageExtension);
                Scenario.install_package(Configuration, "isdependency", "1.0.0");
                Scenario.install_package(Configuration, "hasdependency", "1.0.0");
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
            }

            [Fact]
            public void should_not_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency", "hasdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_not_upgrade_the_minimum_version_dependency()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isdependency", "isdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_not_upgrade_the_exact_version_dependency()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency", "isexactversiondependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_contain_a_message_that_it_was_unable_to_upgrade_anything()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 0/1")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_not_have_a_successful_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Success.ShouldBeFalse();
                }
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Inconclusive.ShouldBeFalse();
                }
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Warning.ShouldBeFalse();
                }
            }

            [Fact]
            public void should_have_an_error_package_result()
            {
                bool errorFound = false;

                foreach (var packageResult in Results)
                {
                    foreach (var message in packageResult.Value.Messages)
                    {
                        if (message.MessageType == ResultType.Error)
                        {
                            errorFound = true;
                        }
                    }
                }

                errorFound.ShouldBeTrue();
            }

            [Fact]
            public void should_have_expected_error_in_package_result()
            {
                bool errorFound = false;

                foreach (var packageResult in Results)
                {
                    foreach (var message in packageResult.Value.Messages)
                    {
                        if (message.MessageType == ResultType.Error)
                        {
                            if (message.Message.Contains("Unable to resolve dependency 'isexactversiondependency")) errorFound = true;
                        }
                    }
                }

                errorFound.ShouldBeTrue();
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_package_with_unavailable_dependencies_ignoring_dependencies : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "hasdependency";
                Scenario.add_packages_to_source_location(Configuration, "hasdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isexactversiondependency.1*" + Constants.PackageExtension);
                Scenario.install_package(Configuration, "isdependency", "1.0.0");
                Scenario.install_package(Configuration, "hasdependency", "1.0.0");
                Configuration.IgnoreDependencies = true;
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency", "hasdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("2.1.0.0");
            }

            [Fact]
            public void should_not_upgrade_the_minimum_version_dependency()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isdependency", "isdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_not_upgrade_the_exact_version_dependency()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency", "isexactversiondependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_contain_a_message_that_it_upgraded_only_the_package_successfully()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 1/1")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Success.ShouldBeTrue();
                }
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Inconclusive.ShouldBeFalse();
                }
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Warning.ShouldBeFalse();
                }
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_dependency_happy : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "isdependency";
                Scenario.add_packages_to_source_location(Configuration, "hasdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isdependency.1*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isexactversiondependency*" + Constants.PackageExtension);
                Scenario.install_package(Configuration, "isdependency", "1.0.0");
                Scenario.install_package(Configuration, "hasdependency", "1.0.0");
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isdependency", "isdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.1.0.0");
            }

            [Fact]
            public void should_not_upgrade_the_parent_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency", "hasdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_not_upgrade_the_exact_version_dependency()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency", "isexactversiondependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_contain_a_message_the_dependency_upgraded_successfully()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 1/1")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Success.ShouldBeTrue();
                }
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Inconclusive.ShouldBeFalse();
                }
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Warning.ShouldBeFalse();
                }
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_dependency_legacy_folder_version : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "isdependency";
                Scenario.add_packages_to_source_location(Configuration, "hasdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isdependency.1*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isexactversiondependency*" + Constants.PackageExtension);
                Configuration.AllowMultipleVersions = true;
                Scenario.install_package(Configuration, "isdependency", "1.0.0");
                Scenario.install_package(Configuration, "hasdependency", "1.0.0");
                Configuration.AllowMultipleVersions = false;

                string dotChocolatey = Path.Combine(Scenario.get_top_level(), ".chocolatey");
                if (Directory.Exists(dotChocolatey))
                {
                    Directory.Delete(dotChocolatey, recursive: true);
                }
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isdependency", "isdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.1.0.0");
            }

            [Fact]
            public void should_remove_the_legacy_folder_version_of_the_package()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "isdependency.1.0.0");
                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_replace_the_legacy_folder_version_of_the_package_with_a_lib_package_folder_that_has_no_version()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "isdependency");
                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_not_upgrade_the_parent_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency.1.0.0", "hasdependency.1.0.0.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_not_add_a_versionless_parent_package_folder_to_the_lib_dir()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency");
                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_leave_the_parent_package_as_legacy_folder()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency.1.0.0");
                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_not_upgrade_the_exact_version_dependency()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency.1.0.0", "isexactversiondependency.1.0.0.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.0.0");
            }

            [Fact]
            public void should_leave_the_exact_version_package_as_legacy_folder()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency.1.0.0");
                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_not_add_a_versionless_exact_version_package_folder_to_the_lib_dir()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency");
                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_contain_a_message_the_dependency_upgraded_successfully()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 1/1")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Success.ShouldBeTrue();
                }
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Inconclusive.ShouldBeFalse();
                }
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Warning.ShouldBeFalse();
                }
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_dependency_with_parent_that_depends_on_a_range_less_than_upgrade_version : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "isdependency";
                Scenario.add_packages_to_source_location(Configuration, "hasdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isexactversiondependency*" + Constants.PackageExtension);
                Scenario.install_package(Configuration, "isdependency", "1.0.0");
                Scenario.install_package(Configuration, "hasdependency", "1.0.0");
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isdependency", "isdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("2.1.0.0");
            }

            [Fact]
            public void should_upgrade_the_parent_package_to_lowest_version_that_meets_new_dependency_version()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency", "hasdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.1.0");
            }

            [Fact]
            public void should_upgrade_the_exact_version_dependency()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency", "isexactversiondependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.1.0");
            }

            [Fact]
            public void should_contain_a_message_that_everything_upgraded_successfully()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 3/3")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Success.ShouldBeTrue();
                }
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Inconclusive.ShouldBeFalse();
                }
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Warning.ShouldBeFalse();
                }
            }
        }

        [Concern(typeof (ChocolateyUpgradeCommand))]
        public class when_upgrading_a_legacy_folder_dependency_with_parent_that_depends_on_a_range_less_than_upgrade_version : ScenariosBase
        {
            public override void Context()
            {
                base.Context();
                Configuration.PackageNames = Configuration.Input = "isdependency";
                Scenario.add_packages_to_source_location(Configuration, "hasdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isdependency.*" + Constants.PackageExtension);
                Scenario.add_packages_to_source_location(Configuration, "isexactversiondependency*" + Constants.PackageExtension);
                Configuration.AllowMultipleVersions = true;
                Scenario.install_package(Configuration, "isdependency", "1.0.0");
                Scenario.install_package(Configuration, "hasdependency", "1.0.0");
                Configuration.AllowMultipleVersions = false;

                string dotChocolatey = Path.Combine(Scenario.get_top_level(), ".chocolatey");
                if (Directory.Exists(dotChocolatey))
                {
                    Directory.Delete(dotChocolatey, recursive: true);
                }
            }

            public override void Because()
            {
                Results = Service.upgrade_run(Configuration);
            }

            [Fact]
            public void should_upgrade_the_package()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isdependency", "isdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("2.1.0.0");
            }

            [Fact]
            public void should_remove_the_legacy_folder_version_of_the_package()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "isdependency.1.0.0");
                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_replace_the_legacy_folder_version_of_the_package_with_a_lib_package_folder_that_has_no_version()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "isdependency");
                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            public void should_upgrade_the_parent_package_to_lowest_version_that_meets_new_dependency_version()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency", "hasdependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.1.0");
            }

            [Fact]
            public void should_replace_the_legacy_folder_version_of_the_parent_package_with_a_lib_package_folder_that_has_no_version()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency");
                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            [Pending("Legacy packages are left when implicit - GH-117")]
            public void should_remove_the_legacy_folder_version_of_the_parent_package()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "hasdependency.1.0.0");
                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_upgrade_the_exact_version_dependency()
            {
                var packageFile = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency", "isexactversiondependency.nupkg");
                var package = new OptimizedZipPackage(packageFile);
                package.Version.Version.to_string().ShouldEqual("1.0.1.0");
            }

            [Fact]
            public void should_replace_the_legacy_folder_version_of_the_exact_version_package_with_a_lib_package_folder_that_has_no_version()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency");
                Directory.Exists(packageDir).ShouldBeTrue();
            }

            [Fact]
            [Pending("Legacy packages are left when implicit - GH-117")]
            public void should_remove_the_legacy_folder_version_of_the_exact_version_package()
            {
                var packageDir = Path.Combine(Scenario.get_top_level(), "lib", "isexactversiondependency.1.0.0");
                Directory.Exists(packageDir).ShouldBeFalse();
            }

            [Fact]
            public void should_contain_a_message_that_everything_upgraded_successfully()
            {
                bool expectedMessage = false;
                foreach (var message in MockLogger.MessagesFor(LogLevel.Warn).or_empty_list_if_null())
                {
                    if (message.Contains("upgraded 3/3")) expectedMessage = true;
                }

                expectedMessage.ShouldBeTrue();
            }

            [Fact]
            public void should_have_a_successful_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Success.ShouldBeTrue();
                }
            }

            [Fact]
            public void should_not_have_inconclusive_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Inconclusive.ShouldBeFalse();
                }
            }

            [Fact]
            public void should_not_have_warning_package_result()
            {
                foreach (var packageResult in Results)
                {
                    packageResult.Value.Warning.ShouldBeFalse();
                }
            }
        }
    }
}
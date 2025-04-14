var status = _fileUtility.ParseXml(siteFile, out var siteDoc);
            if (status.IsFailed())
                return status;

            StringBuilder systemNumberNotFound = new StringBuilder("");
            foreach (var device in turbines.GetControllerDevices())
            {
                if (string.IsNullOrEmpty(device.Name))
                {
                    _loggerService.Error($"The device name is empty");
                    continue;
                }

                device.SystemNumber = siteDoc.Descendants("Turbines").Descendants("Turbine")
                                        .FirstOrDefault(x => SystemNumberCondition(x, device.Name, device.Ip))
                                        ?.Attribute("systemNumber").Value;

                if (string.IsNullOrWhiteSpace(device.SystemNumber))
                    systemNumberNotFound.Append($"{device.Name}, ");
                else
                {
                    var converter = turbines.GetConverterDevice(device.Name);

                    _cacheManager.Set<string>(GlobalCacheKeys.SystemNumber.For($"{device.Name}"), device.SystemNumber);
                    _cacheManager.Set<string>(GlobalCacheKeys.SystemNumber.For($"{converter.Name}"), device.SystemNumber);
                }
            }

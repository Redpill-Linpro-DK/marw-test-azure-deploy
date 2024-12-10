# Azure VNet, Subnet, and NAT Gateway Configuration - Onprem IP-Whitelisting

This Bicep template provisions an Azure Virtual Network (VNet) with a subnet and a NAT Gateway, configured for environment-specific settings such as Development, Testing, and Production. 
The configuration includes the use of a static public IP that enables IP whitelisting for secure communication with external resources.

## Table of Contents
1. [Parameters](#1parameters)
2. [Resource Configuration](#2resource-configuration)
   - 2.1 [Public IP](#21public-ip)
   - 2.2 [NAT Gateway](#22nat-gateway)
   - 2.3 [Virtual Network (VNet) and Subnet](#23virtual-network-vnet-and-subnet)

---

## 1.Parameters

| Parameter               | Type   | Description                                                                           |
|:------------------------|--------|---------------------------------------------------------------------------------------|
| **applicationName**     | string | Application name used for naming resources.                                           |
| **env**                 | string | Deployment environment identifier (`dev`, `test`, `production`).                      |
| **postfixCount**        | string | Numeric postfix to ensure unique naming.                                              |
| **uniqueDeployId**      | string | Unique identifier for deployment.                                                     |
| **internalNetworkName** | string | Internal network name, included in the resource naming convention.                    |
| **location**            | string | Azure region for resource deployment.                                                 |
| **tags**                | object | Metadata tags applied to all resources.                                               |
| **addressSpace**        | string | Address space for the VNet. See [Vnet Documentation](./vnet.md) for default values    |
| **subnetPrefix**        | string | Subnet address prefix.See [Vnet Documentation](./vnet.md) for default values          |
| **vnetName**            | string | Name of the VNet, generated based on naming convention if not specified.              |
| **subnetName**          | string | Name of the subnet, generated based on naming convention if not specified.            |
| **publicIpName**        | string | Name for the static Public IP, generated based on naming convention if not specified. |
| **natGwName**           | string | NAT Gateway name, generated based on naming convention if not specified.              | 
| **skuName**             | string | SKU name for the Public IP and NAT Gateway, defaulting to `Standard`.                 |
| **skuTier**             | string | SKU tier for Public IP and NAT Gateway, defaulting to `Regional`.                     |


## 2.&nbsp;Resource Configuration

### 2.1&nbsp;Public IP

The template provisions a static Public IP for outbound traffic from resources within the subnet. This IP is associated with the NAT Gateway and enables IP whitelisting for secure communication with external resources.

- **SKU**: Configurable via the `skuName` and `skuTier` parameters, defaulting to `Standard` and `Regional`.
- **Public IP Allocation**: Set to `Static`, ensuring a consistent public IP for outbound traffic.
- **IP Version**: Configured as IPv4.

**Microsoft Documentation**: [Azure Public IP Overview](https://learn.microsoft.com/en-us/azure/virtual-network/public-ip-addresses)
**Microsoft Bicep Documentation**: [Azure Public IP Bicep Documentation](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/publicipaddresses?pivots=deployment-language-bicep)

### 2.2&nbsp;NAT Gateway

The NAT Gateway provides a consistent IP for all outbound traffic from resources connected to the specified subnet, ensuring a whitelisted IP for secure interactions with external services. Key configuration aspects include:

- **Idle Timeout**: Set to 4 minutes.
- **Associated Public IP**: Links to the `publicIP` resource, providing a single outbound IP.

**Microsoft Documentation**: [Azure NAT Gateway](https://learn.microsoft.com/en-us/azure/virtual-network/nat-gateway/nat-overview)
**Microsoft Bicep Documentation**: [Azure NAT Gateway Bicep Documentation](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/natgateways?pivots=deployment-language-bicep)

### 2.3&nbsp;Virtual Network (VNet) and Subnet

The Vnet configuration is explained here: [Vnet Documentation](./vnet.md).
# Azure VNet and Subnet Configuration for Environment-Specific Deployments

This Bicep template provisions an Azure Virtual Network (VNet) with an associated subnet, tailored for different environments such as Development, Testing, and Production.
The template dynamically selects address spaces and subnet prefixes based on the deployment environment.

## Table of Contents
1. [Parameters](#1parameters)
2. [Dynamic Address Spaces and Subnet Prefixes](#2dynamic-address-spaces-and-subnet-prefixes)
   - 2.1 [Environment-Specific Address Spaces](#21environment-specific-address-spaces)
   - 2.2 [Environment-Specific Subnet Prefixes](#22environment-specific-subnet-prefixes)
3. [Resource Configuration](#3resource-configuration)
    - 3.1 [Virtual Network (VNet)](#31virtual-network-vnet)
    - 3.2 [Subnet Configuration](#32subnet-configuration)
      - 3.2.1 [Private Endpoint Network Policies](#321private-endpoint-network-policies)
      - 3.2.2 [Private Link Service Network Policies](#322private-link-service-network-policies)

---

## 1.&nbsp;Parameters

| Parameter               | Type   | Description                                                                                                                     |
|-------------------------|--------|---------------------------------------------------------------------------------------------------------------------------------|
| **addressSpace**        | string | Custom address space for the VNet. Defaults to a specific range based on the environment (`env`) if not provided.               |
| **applicationName**     | string | Application name for naming resources                                                                                           |
| **env**                 | string | Environment identifier (`dev`, `test`, `production`), which dictates dynamic address space, subnet prefix selection and naming. |
| **internalNetworkName** | string | Internal network name used in resource naming conventions.                                                                      |
| **location**            | string | Azure region for deployment.                                                                                                    |
| **postfixCount**        | string | Numeric postfix for unique naming.                                                                                              |
| **subnetName**          | string | Name of the subnet, automatically generated if not specified, using a naming template.                                          |
| **subnetPrefix**        | string | Custom subnet prefix, defaults based on `env` if left blank.                                                                    |
| **tags**                | object | Metadata tags applied to VNet and subnet resources.                                                                             |
| **uniqueDeployId**      | string | Unique deployment identifier to ensure unique names across resources.                                                           |
| **vnetName**            | string | Name of the VNet, generated based on naming template if not provided.                                                           |

## 2.&nbsp;Dynamic Address Spaces and Subnet Prefixes

The template leverages dynamic variables to assign address spaces and subnet prefixes based on the environment, streamlining IP management across Development, Testing, and Production.

### 2.1&nbsp;Environment-Specific Address Spaces

The address space for each environment defaults to the following values:
- **Dev**: `10.0.0.0/16`
- **Test**: `10.1.0.0/16`
- **Production**: `10.2.0.0/16`

This setup enables controlled IP range allocation, reducing the risk of conflicts across environments.

### 2.2&nbsp;Environment-Specific Subnet Prefixes

Similarly, subnet prefixes defaults based on the environment:
- **Dev**: `10.0.0.0/24`
- **Test**: `10.1.0.0/24`
- **Production**: `10.2.0.0/24`

## 3.&nbsp;Resource Configuration

### 3.1&nbsp;Virtual Network (VNet)

- **Address Space**: Selected based on environment (`dev`, `test`, `production`) or custom `addressSpace`.
- **Subnet Configuration**:
    - **Private Endpoint Policies**: Set to `'Disabled'`.
    - **Private Link Policies**: Set to `'Enabled'`.
    - **Delegation**: Includes a delegation for `Microsoft.Web/serverFarms`, enabling integration with Azure services like App Service and Functions.
    - 
- **Microsoft Documentation**: [Azure Virtual Network Overview](https://learn.microsoft.com/en-us/azure/virtual-network/virtual-networks-overview)
- **Microsoft Bicep Documentation**: [Azure Virtual Network Bicep Documentation](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/virtualnetworks?pivots=deployment-language-bicep)

### 3.2&nbsp;Subnet Configuration

The subnet within the VNet supports environment-based IP address ranges and specific network policies to support secure connectivity options.

- **Microsoft Documentation**: [Subnets in VNets](https://learn.microsoft.com/en-us/azure/virtual-network/virtual-network-manage-subnet)
- **Microsoft Bicep Documentation**: Is part of the **Microsoft Vnet Bicep Documentation**

#### 3.2.1&nbsp;Private Endpoint Network Policies

- **Setting**: `privateEndpointNetworkPolicies` is set to `'Disabled'`.
- **Purpose**: A property of each subnet in a virtual network (VNet) that determines whether network policies are enabled or disabled for Private Endpoints in that subnet.

**Microsoft Documentation**: [Private Endpoint Overview](https://learn.microsoft.com/en-us/azure/private-link/private-endpoint-overview)

#### 3.2.2&nbsp;Private Link Service Network Policies

- **Setting**: `privateLinkServiceNetworkPolicies` is set to `'Enabled'`.
- **Purpose**: A property of each subnet in a virtual network (VNet) that determines whether network policies are enabled or disabled for Azure Private Link services.

**Microsoft Documentation**: [Private Link Service Overview](https://learn.microsoft.com/en-us/azure/private-link/private-link-service-overview)
# Azure Spoke VNet Configuration with NAT Gateway, Route Table, and NSG

This Bicep template provisions an Azure Virtual Network (VNet) in a spoke configuration, designed to integrate with a hub-and-spoke network topology.
The spoke VNet includes a subnet with a NAT Gateway, Route Table, and Network Security Group (NSG) for controlled access and secure connectivity to an on-premises network or other peered networks.

## Table of Contents

1. [Parameters](#1parameters)
2. [Dynamic Address Spaces and Subnet Prefixes](#2dynamic-address-spaces-and-subnet-prefixes)
   - 2.1 [Environment-Specific Address Spaces](#21environment-specific-address-spaces)
   - 2.2 [Environment-Specific Subnet Prefixes](#22environment-specific-subnet-prefixes)
3. [Resource Configuration](#3resource-configuration)
   - 3.1 [Public IP](#31public-ip)
   - 3.2 [NAT Gateway](#32nat-gateway)
   - 3.3 [Route Table](#33route-table)
   - 3.4 [Virtual Network (VNet) and Subnet](#34virtual-network-vnet-and-subnet)
   - 3.5 [Subnet Configuration](#35subnet-configuration)
     - 3.5.1 [Private Endpoint Network Policies](#351private-endpoint-network-policies)
     - 3.5.2 [Private Link Service Network Policies](#352private-link-service-network-policies)
    - 3.6 [Network Security Group (NSG) and Rules](#36network-security-group-nsg-and-rules)


---

## 1.&nbsp;Parameters

| Parameter               | Type   | Description                                                                                                                     |
|-------------------------|--------|---------------------------------------------------------------------------------------------------------------------------------|
| **addressSpace**        | string | Custom address space for the VNet. Defaults to a specific range based on the environment (`env`) if not provided.               |
| **applicationName**     | string | Name of the application, used in resource names.                                                                                |
| **env**                 | string | Environment identifier (`dev`, `test`, `production`), which dictates dynamic address space, subnet prefix selection and naming. |
| **internalNetworkName** | string | Internal network name used in resource naming conventions.                                                                      |
| **location**            | string | Azure region for deployment.                                                                                                    |
| **postfixCount**        | string | Numeric postfix for unique naming.                                                                                              |
| **subnetName**          | string | Name of the subnet, generated based on naming convention if not specified.                                                      |
| **subnetPrefix**        | string | Custom subnet prefix, defaults based on `env` if left blank.                                                                    |
| **tags**                | object | Metadata tags applied to VNet and subnet resources.                                                                             |
| **uniqueDeployId**      | string | Unique deployment identifier to ensure unique names across resources.                                                           |
| **vnetName**            | string | Name of the VNet, generated based on naming template if not provided.                                                           |
| **publicIpName**        | string | Name for the static Public IP, generated based on naming convention if not specified.                                           |
| **natGwName**           | string | NAT Gateway name, generated based on naming convention if not specified.                                                        | 
| **skuName**             | string | SKU name for Public IP and NAT Gateway, defaulting to `Standard`.                                                               |
| **skuTier**             | string | SKU tier for Public IP and NAT Gateway, defaulting to `Regional`.                                                               |
| **routeTableName**      | string | Name of the Route Table, generated based on naming template if not provided.                                                    |
| **nextHopIpAddress**    | string | IP address for next hop, typically the hub network’s IP address.                                                                |
| **routeAddressPrefix**  | string | Address prefix for routing traffic to the hub.                                                                                  |
| **nsgName**             | string | Name of the Network Security Group, generated based on naming template if not provided.                                         |

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

### 3.1&nbsp;Public IP

The template provisions a static Public IP for outbound traffic from resources within the subnet. This IP is associated with the NAT Gateway and enables IP whitelisting for secure communication with external resources.

- **SKU**: Configurable via the `skuName` and `skuTier` parameters, defaulting to `Standard` and `Regional`.
- **Public IP Allocation**: Set to `Static`, ensuring a consistent public IP for outbound traffic.
- **IP Version**: Configured as IPv4.

**Microsoft Documentation**: [Azure Public IP Overview](https://learn.microsoft.com/en-us/azure/virtual-network/public-ip-addresses)
**Microsoft Bicep Documentation**: [Azure Public IP Bicep Documentation](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/publicipaddresses?pivots=deployment-language-bicep)

### 3.2&nbsp;NAT Gateway

The NAT Gateway provides a consistent IP for all outbound traffic from resources connected to the specified subnet, ensuring a whitelisted IP for secure interactions with external services. Key configuration aspects include:

- **Idle Timeout**: Set to 4 minutes.
- **Associated Public IP**: Links to the `publicIP` resource, providing a single outbound IP.

**Microsoft Documentation**: [Azure NAT Gateway](https://learn.microsoft.com/en-us/azure/virtual-network/nat-gateway/nat-overview)
**Microsoft Bicep Documentation**: [Azure NAT Gateway Bicep Documentation](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/natgateways?pivots=deployment-language-bicep)

### 3.3&nbsp;Route Table

The **Route Table** directs outbound traffic through the hub VNet by configuring a specific route for the specified `routeAddressPrefix`.
This route table is used for spoke VNets that need controlled access to a central hub or on-premises network.

- **Route Name**: `On-Prem`
- **Next Hop Type**: `VirtualAppliance`, change according to needs.
- **Address Prefix**: Default is set to `10.0.0.0/8`, configurable to match the specific hub or on-premises network address space.

**Microsoft Documentation**: [Route Tables](https://learn.microsoft.com/en-us/azure/virtual-network/manage-route-table)
**Microsoft Bicep Documentation**: [Route Tables Bicep Documentation](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/routetables?pivots=deployment-language-bicep)

### 3.4&nbsp;Virtual Network (VNet) and Subnet

The **VNet** is configured to support a spoke network in a hub-and-spoke topology.
It dynamically selects address spaces and subnet prefixes based on the environment, with specific network policies and NAT configuration in place.

- **Address Space**: Selected based on environment (`dev`, `test`, `production`) or custom `addressSpace`.
- **Subnet Configuration**:
  - **Network Security Group**: Configured to control inbound and outbound traffic.
  - **NAT Gateway**: Routes outbound traffic through a static Public IP.
  - **Route Table**: Configured for routing traffic to the hub network.
  - **Private Endpoint and Private Link Policies**: Both are set to `'Enabled'` using the NSG.
  - **Delegation**: Includes a delegation for `Microsoft.Web/serverFarms`, enabling integration with Azure services like App Service and Functions.

- **Microsoft Documentation**: [Azure Virtual Network Overview](https://learn.microsoft.com/en-us/azure/virtual-network/virtual-networks-overview)
- **Microsoft Bicep Documentation**: [Azure Virtual Network Bicep Documentation](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/virtualnetworks?pivots=deployment-language-bicep)

### 3.5&nbsp;Subnet Configuration

The subnet within the VNet supports environment-based IP address ranges and specific network policies to support secure connectivity options.

- **Microsoft Documentation**: [Subnets in VNets](https://learn.microsoft.com/en-us/azure/virtual-network/virtual-network-manage-subnet)
- **Microsoft Bicep Documentation**: Is part of the **Microsoft Vnet Bicep Documentation**

#### 3.5.1&nbsp;Private Endpoint Network Policies

- **Setting**: `privateEndpointNetworkPolicies` is set to `'Enabled'`.
- **Purpose**: A property of each subnet in a virtual network (VNet) that determines whether network policies are enabled or disabled for Private Endpoints in that subnet.

**Microsoft Documentation**: [Private Endpoint Overview](https://learn.microsoft.com/en-us/azure/private-link/private-endpoint-overview)

#### 3.5.2&nbsp;Private Link Service Network Policies

- **Setting**: `privateLinkServiceNetworkPolicies` is set to `'Enabled'`.
- **Purpose**: A property of each subnet in a virtual network (VNet) that determines whether network policies are enabled or disabled for Azure Private Link services.

**Microsoft Documentation**: [Private Link Service Overview](https://learn.microsoft.com/en-us/azure/private-link/private-link-service-overview)

### 3.6&nbsp;Network Security Group (NSG) and Rules

The **Network Security Group (NSG)** secures the subnet by restricting traffic based on custom rules. 
The rule `AllowOutToOnPrem` specifically allows outbound traffic from the spoke VNet to the hub or on-premises network.

- **AllowOutToOnPrem** Rule:
  - **Direction**: Outbound, allowing traffic from the subnet to the hub.
  - **Protocol**: All protocols are allowed (`*`).
  - **Source/Destination**: Allows traffic to the hub address space (`routeAddressPrefix`) over ports 80 and 443.
  - **Priority**: Set to 120, enabling higher-priority rules if needed.

**Microsoft Documentation**: [Network Security Groups](https://learn.microsoft.com/en-us/azure/virtual-network/network-security-groups-overview)
**Microsoft Bicep Documentation**: [Network Security Groups Bicep Documentation](https://learn.microsoft.com/en-us/azure/templates/microsoft.network/networksecuritygroups?pivots=deployment-language-bicep)
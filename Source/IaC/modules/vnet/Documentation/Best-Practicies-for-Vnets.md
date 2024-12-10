# Azure Virtual Network (VNet) Best Practices for Multiple Environments (Dev, Test, Prod)

This document outlines best practices for setting up and managing Azure Virtual Networks (VNets) across different environments—Development, Testing, and Production.

## Table of Contents
1. [Separate VNets for Each Environment](#1separate-vnets-for-each-environment)
2. [Use Network Security Groups (NSGs) for Access Control](#2use-network-security-groups-nsgs-for-access-control)
3. [IP Address Management and Subnetting](#3ip-address-management-and-subnetting)
4. [DNS Configuration](#4dns-configuration)
5. [VNet Peering for Controlled Connectivity with External Systems](#5vnet-peering-for-controlled-connectivity-with-external-systems)
6. [Utilize Firewalls for Layered Security](#6utilize-firewalls-for-layered-security)

---

## 1.Separate VNets for Each Environment

### Best Practice
Create distinct VNets for each environment to enforce isolation and enhance security. This setup reduces the risk of accidental exposure of resources between environments, as each environment (Dev, Test, Prod) will operate in a separate network space, minimizing cross-environment dependencies and allowing for environment-specific access policies.

**Further Reading**: [Create, configure, and manage Azure Virtual Networks](https://learn.microsoft.com/en-us/azure/virtual-network/virtual-network-manage-network)

---

## 2.Use Network Security Groups (NSGs) for Access Control

### Best Practice
Apply Network Security Groups (NSGs) to subnets and/or VMs within each VNet to control inbound and outbound traffic. Environment-specific NSG configurations can help ensure appropriate security levels across Dev, Test, and Prod environments. For example:
- **Development (Dev)**: May allow broader access for testing and development flexibility.
- **Testing (Test)**: Should restrict access to authorized testing IPs.
- **Production (Prod)**: Strictly restrict access to necessary IPs only, typically through specific corporate or management IP ranges.

**Further Reading**: [Network security groups (NSG) in Azure](https://learn.microsoft.com/en-us/azure/virtual-network/security-overview)

### Example NSG Rules

#### Development NSG Rules
```plaintext
- Allow HTTP/HTTPS traffic from Dev team IP addresses.
- Deny all other inbound traffic from external networks, except required Dev IPs.
```

#### Production NSG Rules
```plaintext
- Allow HTTP/HTTPS traffic only from known corporate IP addresses.
- Deny all other inbound traffic by default, except for necessary management ports.
```

---

## 3.IP Address Management and Subnetting

### Best Practice
Assign unique IP address ranges to each VNet and subnet within each environment to avoid conflicts and ensure clear IP allocation. Subnet segregation within each environment (e.g., Web, App, and DB subnets) further improves manageability and security.

**Further Reading**: [Address space and subnets in Azure](https://learn.microsoft.com/en-us/azure/virtual-network/virtual-networks-ip-addresses-overview-arm)

### Example IP Ranges

```plaintext
- Dev VNet: 10.1.0.0/16
   - Web Subnet: 10.1.1.0/24
   - App Subnet: 10.1.2.0/24
   - DB Subnet: 10.1.3.0/24

- Test VNet: 10.2.0.0/16
   - Web Subnet: 10.2.1.0/24
   - App Subnet: 10.2.2.0/24
   - DB Subnet: 10.2.3.0/24

- Prod VNet: 10.3.0.0/16
   - Web Subnet: 10.3.1.0/24
   - App Subnet: 10.3.2.0/24
   - DB Subnet: 10.3.3.0/24
```

---

## 4.DNS Configuration

### Best Practice
Use Azure Private DNS zones or a custom DNS server to handle internal name resolution across environments. Each environment (Dev, Test, Prod) can have a unique DNS zone to avoid overlap and ensure traffic is directed correctly within the environment.

**Further Reading**: [Private DNS zones in Azure](https://learn.microsoft.com/en-us/azure/dns/private-dns-overview)

### Example DNS Setup

```plaintext
- Dev DNS Zone: dev.mycompany.local
- Test DNS Zone: test.mycompany.local
- Prod DNS Zone: prod.mycompany.local
```

In each environment:
1. Configure private DNS zones for internal name resolution, enabling consistent service discovery across each environment.
2. Link each DNS zone to its corresponding VNet for isolated name resolution, preventing Dev/Test/Prod from resolving each other's internal addresses.

---

## 5.VNet Peering for Controlled Connectivity with External Systems

### Best Practice
Implement VNet peering only between systems that require direct communication, such as central shared services, authentication, or logging systems. Avoid unnecessary peering between Dev, Test, and Prod VNets to maintain isolation. Use Network Security Groups (NSGs) and route tables to enforce controlled access and minimize security risks between systems.

**Further Reading**: [Virtual network peering](https://learn.microsoft.com/en-us/azure/virtual-network/virtual-network-peering-overview)

### Example Peering Scenarios
1. **Dev to Shared Services**: Peering can allow the Dev environment to access shared resources (e.g., logging or monitoring) while restricting access to Prod resources.
2. **Prod to Core Services Only**: Prod VNet peering should only connect to essential core services, preventing access from non-essential systems to enhance security.

---

## 6.Utilize Firewalls for Layered Security

### Best Practice
Deploy Azure Firewall for centralized traffic control, particularly in Production environments. A centralized firewall can filter and control inbound and outbound traffic at the VNet level, adding an additional layer of security beyond NSGs. Azure Firewall can also enable logging and analytics to monitor traffic patterns and detect potential security incidents.

**Further Reading**: [Azure Firewall documentation](https://learn.microsoft.com/en-us/azure/firewall/overview)

### Configuration Recommendations for Azure Firewall

#### 1. **Inbound Protection**: Use Azure Firewall for production VNets to protect applications and services from unauthorized external access.
- **Public IP Association**: Assign a static public IP to Azure Firewall to allow consistent IP whitelisting.
- **DNAT Rules**: Configure DNAT rules to allow only required inbound traffic to specific internal resources based on IP and port.

#### 2. **Outbound Control**: Route all outbound traffic through Azure Firewall to control access to the internet and external services.
- **Application Rules**: Define application rules to allow outbound traffic only to approved domains or IP addresses, such as specific API endpoints.
- **Network Rules**: Use network rules to enforce IP-based filtering for outbound connections, providing granular control.

#### 3. **Logging and Analytics**: Enable diagnostic logging and integrate Azure Firewall logs with Azure Monitor and Log Analytics for real-time monitoring and alerts.
- **Traffic Analysis**: Use logging to analyze traffic patterns, identify unusual activity, and optimize security configurations.
- **Alerts and Notifications**: Configure alerts for abnormal traffic spikes or unusual access patterns to enable rapid response to potential security incidents.`

#### Benefits of Using Azure Firewall in Production

- **Centralized Security Control**: Enforces traffic filtering across subnets and regions.
- **Consistent Outbound IP Addressing**: Ensures all outbound traffic uses a known IP, simplifying IP whitelisting for external services.
- **Enhanced Monitoring**: Provides insights into traffic patterns and potential security risks.
